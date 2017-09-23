using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JsonRpc.Server.Messages;
using Newtonsoft.Json.Linq;

namespace JsonRpc
{
    public class InputHandler : IInputHandler
    {
        public const char CR = '\r';
        public const char LF = '\n';
        public static char[] CRLF = { CR, LF };
        public static char[] HeaderKeys = { CR, LF, ':' };
        public const short MinBuffer = 21; // Minimum size of the buffer "Content-Length: X\r\n\r\n"

        private readonly Stream _input;
        private readonly IOutputHandler _outputHandler;
        private readonly IReciever _reciever;
        private readonly IRequestProcessIdentifier _requestProcessIdentifier;
        private Thread _inputThread;
        private readonly IRequestRouter _requestRouter;
        private readonly IResponseRouter _responseRouter;
        private readonly IScheduler _scheduler;

        public InputHandler(
            Stream input,
            IOutputHandler outputHandler,
            IReciever reciever,
            IRequestProcessIdentifier requestProcessIdentifier,
            IRequestRouter requestRouter,
            IResponseRouter responseRouter
            )
        {
            if (!input.CanRead) throw new ArgumentException($"must provide a readable stream for {nameof(input)}", nameof(input));
            _input = input;
            _outputHandler = outputHandler;
            _reciever = reciever;
            _requestProcessIdentifier = requestProcessIdentifier;
            _requestRouter = requestRouter;
            _responseRouter = responseRouter;

            _scheduler = new ProcessScheduler();
            _inputThread = new Thread(ProcessInputStream) { IsBackground = true, Name = "ProcessInputStream" };
        }

        Serilog.ILogger Log { get; } = Serilog.Log.ForContext<InputHandler>();

        public void Start()
        {
            Log.Verbose("Starting input handler...");

            _outputHandler.Start();
            _inputThread.Start();
            _scheduler.Start();

            Log.Verbose("Started input handler.");
        }

        // don't be async: We already allocated a seperate thread for this.
        private void ProcessInputStream()
        {
            // some time to attach a debugger
            //System.Threading.Thread.Sleep(TimeSpan.FromSeconds(5));

            // header is encoded in ASCII
            // "Content-Length: 0" counts bytes for the following content
            // content is encoded in UTF-8
            while (true)
            {
                if (_inputThread == null) return;

                Log.Verbose("Starting read from input stream...");

                var buffer = new byte[300];
                var current = _input.Read(buffer, 0, MinBuffer);

                Log.Verbose("Read {ByteCount} bytes from input stream.", current);

                if (current == 0)
                {
                    Log.Warning("ProcessInputStream: Current = 0");

                    Thread.Sleep(1000);

                    continue;
                }

                while (current < MinBuffer || 
                       buffer[current - 4] != CR || buffer[current - 3] != LF ||
                       buffer[current - 2] != CR || buffer[current - 1] != LF)
                {
                    Log.Verbose("Reading additional data from input stream...");

                    var n = _input.Read(buffer, current, 1);
                    if (n == 0) return; // no more _input, mitigates endless loop here.

                    Log.Verbose("Read {ByteCount} bytes of additional data from input stream.", n);

                    current += n;
                }

                var headersContent = System.Text.Encoding.ASCII.GetString(buffer, 0, current);
                Log.Verbose("Got raw headers: {Headers}", headersContent);

                var headers = headersContent.Split(HeaderKeys, StringSplitOptions.RemoveEmptyEntries);
                long length = 0;
                for (var i = 1; i < headers.Length; i += 2)
                {
                    // starting at i = 1 instead of 0 won't throw, if we have uneven headers' length
                    var header = headers[i - 1];
                    var value = headers[i].Trim();
                    if (header.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                    {
                        length = 0;
                        long.TryParse(value, out length);
                    }
                }

                Log.Verbose("Parsed headers (ContentLength = {ContentLength}).", length);

                if (length == 0 || length >= int.MaxValue)
                {
                    Log.Verbose("Invalid content length; treating incoming request as an empty request.");

                    HandleRequest(string.Empty);
                }
                else
                {
                    Log.Verbose("Reading incoming request body ({ContentLength} bytes expected)...", length);

                    var requestBuffer = new byte[length];
                    var received = 0;
                    while (received < length)
                    {
                        Log.Verbose("Reading segment of incoming request body ({ReceivedByteCount} of {TotalByteCount} bytes so far)...", received, length);

                        var n = _input.Read(requestBuffer, received, requestBuffer.Length - received);
                        while (n == 0 && received < length)
                        {
                            Log.Warning("Pausing while reading incoming request body (no_more_input after {ByteCount} bytes)...", received);

                            Thread.Sleep(1500);

                            n = _input.Read(requestBuffer, received, requestBuffer.Length - received);
                        }

                        Log.Verbose("Read segment of incoming request body ({ReceivedByteCount} of {TotalByteCount} bytes so far).", received, length);

                        received += n;
                    }

                    Log.Verbose("Received entire payload ({ReceivedByteCount} bytes).", received);

                    // TODO sometimes: encoding should be based on the respective header (including the wrong "utf8" value)
                    var payload = System.Text.Encoding.UTF8.GetString(requestBuffer);

                    Log.Verbose("Read incoming request body ({ContentLength} bytes, total): {RequestBody}.", received, payload);

                    HandleRequest(payload);
                }
            }
        }

        private void HandleRequest(string request)
        {
            Log.Verbose("Handle request {RequestBody}", request);

            JToken payload;
            try
            {
                payload = JToken.Parse(request);
            }
            catch (Exception parseError)
            {
                Log.Error(parseError, "Failed to parse request {RequestBody}", request);

                _outputHandler.Send(new ParseError());
                return;
            }

            if (!_reciever.IsValid(payload))
            {
                Log.Error("Request {RequestBody} is invalid.", request);

                _outputHandler.Send(new InvalidRequest());
                return;
            }

            var (requests, hasResponse) = _reciever.GetRequests(payload);
            if (hasResponse)
            {
                Log.Verbose("Payload has one or more responses.");

                foreach (var response in requests.Where(x => x.IsResponse).Select(x => x.Response))
                {
                    Log.Verbose("Payload has response {@Response}.", response);

                    var id = response.Id is string s ? long.Parse(s) : response.Id is long l ? l : -1;
                    if (id < 0) continue;

                    var tcs = _responseRouter.GetRequest(id);
                    if (tcs is null) continue;

                    if (response.Error is null)
                    {
                        tcs.SetResult(response.Result);
                    }
                    else
                    {
                        tcs.SetException(new Exception(response.Error));
                    }
                }

                return;
            }

            foreach (var (type, item) in requests.Select(x => (_requestProcessIdentifier.Identify(x), x)))
            {
                if (item.IsRequest)
                {
                    Log.Verbose("Schedule {RequestMethod} request {RequestId}.", item.Request.Method, item.Request.Id);

                    _scheduler.Add(
                        type,
                        async () => {
                            var result = await _requestRouter.RouteRequest(item.Request);
                            _outputHandler.Send(result.Value);
                        }
                    );
                }
                else if (item.IsNotification)
                {
                    Log.Verbose("Schedule {NotificationMethod} notification.", item.Notification.Method);

                    _scheduler.Add(
                        type,
                        () => {
                            _requestRouter.RouteNotification(item.Notification);
                            return Task.CompletedTask;
                        }
                    );
                }
                else if (item.IsError)
                {
                    Log.Verbose("Schedule error {@ErrorMessage}.", item.Error.Message);

                    // TODO:
                    _outputHandler.Send(item.Error);
                }
            }
        }


        public void Dispose()
        {
            _outputHandler.Dispose();
            _inputThread = null;
            _scheduler.Dispose();
        }
    }
}
