using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

namespace JsonRpc
{
    public class OutputHandler : IOutputHandler
    {
        private readonly Stream _output;
        private readonly Thread _thread;
        private readonly BlockingCollection<object> _queue;
        private readonly CancellationTokenSource _cancel;

        public OutputHandler(Stream output)
        {
            if (!output.CanWrite) throw new ArgumentException($"must provide a writable stream for {nameof(output)}", nameof(output));
            _output = output;
            _queue = new BlockingCollection<object>();
            _cancel = new CancellationTokenSource();
            _thread = new Thread(ProcessOutputQueue) { IsBackground = true, Name = "ProcessOutputQueue" };
        }

        Serilog.ILogger Log { get; } = Serilog.Log.ForContext<InputHandler>();

        public void Start()
        {
            Log.Verbose("Starting output handler...");

            _thread.Start();

            Log.Verbose("Started output handler.");
        }

        public void Send(object value)
        {
            _queue.Add(value);
        }

        private void ProcessOutputQueue()
        {
            var token = _cancel.Token;
            try
            {
                while (true)
                {
                    if (_queue.TryTake(out var value, Timeout.Infinite, token))
                    {
                        Log.Verbose("Sending item from output queue...");

                        var content = JsonConvert.SerializeObject(value);
                        var contentBytes = System.Text.Encoding.UTF8.GetBytes(content);

                        // TODO: Is this lsp specific??
                        var sb = new StringBuilder();
                        sb.Append($"Content-Length: {contentBytes.Length}\r\n");
                        sb.Append($"\r\n");
                        var headerBytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());

                        Log.Verbose("Sending content {Content} with headers {Headers} from output queue...",
                            content,
                            sb.ToString()
                        );

                        // only one write to _output
                        using (var ms = new MemoryStream(headerBytes.Length + contentBytes.Length))
                        {
                            ms.Write(headerBytes, 0, headerBytes.Length);
                            ms.Write(contentBytes, 0, contentBytes.Length);

                            Log.Verbose("Writing {ByteCount} bytes to output stream...", ms.Length);

                            _output.Write(ms.ToArray(), 0, (int)ms.Position);

                            Log.Verbose("Wrote {ByteCount} bytes to output stream.", ms.Length);
                        }

                        Log.Verbose("Sent item from output queue ({ContentLength} bytes).", contentBytes.Length);
                    }
                }
            }
            catch (OperationCanceledException ex)
            {
                Log.Error(ex, "Sending of item from output queue was cancelled.");

                if (ex.CancellationToken != token)
                    throw;
                // else ignore. Exceptions: OperationCanceledException - The CancellationToken has been canceled.
            }
        }

        public void Dispose()
        {
            _cancel.Cancel();
            _thread.Join();
            _cancel.Dispose();
            _output.Dispose();
        }
    }
}
