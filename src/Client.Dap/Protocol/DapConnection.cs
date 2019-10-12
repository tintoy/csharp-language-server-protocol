using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.DebugAdapter.Client.Dispatcher;
using OmniSharp.Extensions.DebugAdapter.Client.Handlers;
using OmniSharp.Extensions.DebugAdapter.Protocol.Serialization;

namespace OmniSharp.Extensions.DebugAdapter.Client.Protocol
{
    /// <summary>
    ///     An asynchronous connection using the DAP protocol over <see cref="Stream"/>s.
    /// </summary>
    public sealed class DapConnection
        : IDisposable
    {
        /// <summary>
        ///     The buffer size to use when receiving headers.
        /// </summary>
        const short HeaderBufferSize = 300;

        /// <summary>
        ///     Minimum size of the buffer for receiving headers ("Content-Length: 1\r\n\r\n").
        /// </summary>
        const short MinimumHeaderLength = 21;

        /// <summary>
        ///     The length of time to wait for the outgoing message queue to drain.
        /// </summary>
        public static TimeSpan FlushTimeout { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        ///     The encoding used for message headers.
        /// </summary>
        public static Encoding HeaderEncoding = Encoding.ASCII;

        /// <summary>
        ///     The encoding used for message payloads.
        /// </summary>
        public static Encoding PayloadEncoding = Encoding.UTF8;

        /// <summary>
        ///     The queue of outgoing requests.
        /// </summary>
        readonly BlockingCollection<DapMessage> _outgoing = new BlockingCollection<DapMessage>(new ConcurrentQueue<DapMessage>());

        /// <summary>
        ///     The queue of incoming responses.
        /// </summary>
        readonly BlockingCollection<DapMessage> _incoming = new BlockingCollection<DapMessage>(new ConcurrentQueue<DapMessage>());

        /// <summary>
        ///     <see cref="CancellationTokenSource"/>s representing cancellation of requests from the debug adapter (keyed by request Id).
        /// </summary>
        readonly ConcurrentDictionary<int, CancellationTokenSource> _requestCancellations = new ConcurrentDictionary<int, CancellationTokenSource>();

        /// <summary>
        ///     <see cref="TaskCompletionSource{TResult}"/>s representing completion of responses from the debug adapter (keyed by request Id).
        /// </summary>
        readonly ConcurrentDictionary<int, TaskCompletionSource<DapResponse>> _responseCompletions = new ConcurrentDictionary<int, TaskCompletionSource<DapResponse>>();

        /// <summary>
        ///     The input stream.
        /// </summary>
        readonly Stream _input;

        /// <summary>
        ///     The output stream.
        /// </summary>
        readonly Stream _output;

        /// <summary>
        ///     Has the connection been disposed?
        /// </summary>
        bool _disposed;

        /// <summary>
        ///     The cancellation source for the read and write loops.
        /// </summary>
        CancellationTokenSource _cancellationSource;

        /// <summary>
        ///     Cancellation for the read and write loops.
        /// </summary>
        CancellationToken _cancellation;

        /// <summary>
        ///     A <see cref="Task"/> representing the stopping of the connection's send, receive, and dispatch loops.
        /// </summary>
        Task _hasDisconnectedTask = Task.CompletedTask;

        /// <summary>
        ///     The <see cref="DapDispatcher"/> used to dispatch messages to handlers.
        /// </summary>
        DapDispatcher _dispatcher;

        /// <summary>
        ///     A <see cref="Task"/> representing the connection's receive loop.
        /// </summary>
        Task _sendLoop;

        /// <summary>
        ///     A <see cref="Task"/> representing the connection's send loop.
        /// </summary>
        Task _receiveLoop;

        /// <summary>
        ///     A <see cref="Task"/> representing the connection's dispatch loop.
        /// </summary>
        Task _dispatchLoop;

        /// <summary>
        ///     Create a new <see cref="DapConnection"/>.
        /// </summary>
        /// <param name="loggerFactory">
        ///     The factory for loggers used by the connection and its components.
        /// </param>
        /// <param name="input">
        ///     The input stream.
        /// </param>
        /// <param name="output">
        ///     The output stream.
        /// </param>
        public DapConnection(ILoggerFactory loggerFactory, Stream input, Stream output)
        {
            if (loggerFactory == null)
                throw new ArgumentNullException(nameof(loggerFactory));

            if (input == null)
                throw new ArgumentNullException(nameof(input));

            if (!input.CanRead)
                throw new ArgumentException("Input stream does not support reading.", nameof(input));

            if (output == null)
                throw new ArgumentNullException(nameof(output));

            if (!output.CanWrite)
                throw new ArgumentException("Output stream does not support reading.", nameof(output));

            Log = loggerFactory.CreateLogger<DapConnection>();
            _input = input;
            _output = output;

            // What does client version do? Do we have to negotiate this?
            // The connection may change its Serializer instance once connected; this can be propagated to other components as required.
            Serializer = new DapProtocolSerializer();
            MessageFactory = new DapMessageFactory(Serializer);
        }

        /// <summary>
        ///     Dispose of resources being used by the connection.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            try
            {
                Disconnect();

                _cancellationSource?.Dispose();
            }
            finally
            {
                _disposed = true;
            }
        }

        /// <summary>
        ///     The connection's logger.
        /// </summary>
        ILogger Log { get; }

        /// <summary>
        ///     The JSON serializer used for notification, request, and response payloads.
        /// </summary>
        public DapProtocolSerializer Serializer { get; }

        /// <summary>
        ///     The factory for <see cref="DapMessage"/>s used by the connection.
        /// </summary>
        public DapMessageFactory MessageFactory { get; }

        /// <summary>
        ///     Is the connection open?
        /// </summary>
        public bool IsOpen => _sendLoop != null || _receiveLoop != null || _dispatchLoop != null;

        /// <summary>
        ///     A task that completes when the connection is closed.
        /// </summary>
        public Task HasHasDisconnected => _hasDisconnectedTask;

        /// <summary>
        ///     Register a message handler.
        /// </summary>
        /// <param name="handler">
        ///     The message handler.
        /// </param>
        /// <returns>
        ///     An <see cref="IDisposable"/> representing the registration.
        /// </returns>
        public IDisposable RegisterHandler(IHandler handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            DapDispatcher dispatcher = _dispatcher;
            if (dispatcher == null)
                throw new InvalidOperationException("The connection has not been opened.");

            return dispatcher.RegisterHandler(handler);
        }

        /// <summary>
        ///     Open the connection.
        /// </summary>
        /// <param name="dispatcher">
        ///     The <see cref="DapDispatcher"/> used to dispatch messages to handlers.
        /// </param>
        public void Connect(DapDispatcher dispatcher)
        {
            if (dispatcher == null)
                throw new ArgumentNullException(nameof(dispatcher));

            if (IsOpen)
                throw new InvalidOperationException("Connection is already open.");

            _cancellationSource = new CancellationTokenSource();
            _cancellation = _cancellationSource.Token;

            _dispatcher = dispatcher;
            _dispatcher.Serializer = Serializer;
            _sendLoop = SendLoop();
            _receiveLoop = ReceiveLoop();
            _dispatchLoop = DispatchLoop();

            _hasDisconnectedTask = Task.WhenAll(_sendLoop, _receiveLoop, _dispatchLoop);
        }

        /// <summary>
        ///     Close the connection.
        /// </summary>
        /// <param name="flushOutgoing">
        ///     If <c>true</c>, stop receiving and block until all outgoing messages have been sent.
        /// </param>
        public void Disconnect(bool flushOutgoing = false)
        {
            if (flushOutgoing)
            {
                // Stop receiving.
                _incoming.CompleteAdding();

                // Wait for the outgoing message queue to drain.
                int remainingMessageCount = 0;
                DateTime then = DateTime.Now;
                while (DateTime.Now - then < FlushTimeout)
                {
                    remainingMessageCount = _outgoing.Count;
                    if (remainingMessageCount == 0)
                        break;

                    Thread.Sleep(
                        TimeSpan.FromMilliseconds(200)
                    );
                }

                if (remainingMessageCount > 0)
                    Log.LogWarning("Failed to flush outgoing messages ({RemainingMessageCount} messages remaining).", _outgoing.Count);
            }

            // Cancel all outstanding requests.
            // This should not be necessary because request cancellation tokens should be linked to _cancellationSource, but better to be sure we won't leave a caller hanging.
            foreach (TaskCompletionSource<DapResponse> responseCompletion in _responseCompletions.Values)
            {
                responseCompletion.TrySetException(
                    new OperationCanceledException("The request was canceled because the underlying connection was closed.")
                );
            }

            _cancellationSource?.Cancel();
            _sendLoop = null;
            _receiveLoop = null;
            _dispatchLoop = null;
            _dispatcher = null;
        }

        /// <summary>
        ///     Send an empty notification to the debug adapter.
        /// </summary>
        /// <param name="eventType">
        ///     The notification event type.
        /// </param>
        public void SendEvent(string eventType)
        {
            if (string.IsNullOrWhiteSpace(eventType))
                throw new ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: {nameof(eventType)}.", nameof(eventType));

            if (!IsOpen)
                throw new DapException("Not connected to the debug adapter.");

            _outgoing.TryAdd(
                MessageFactory.Events.Create(eventType)
            );
        }

        /// <summary>
        ///     Send a notification message to the debug adapter.
        /// </summary>
        /// <param name="eventType">
        ///     The notification command name.
        /// </param>
        /// <param name="body">
        ///     The event message body.
        /// </param>
        public void SendEvent(string eventType, object body)
        {
            if (string.IsNullOrWhiteSpace(eventType))
                throw new ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: {nameof(eventType)}.", nameof(eventType));

            if (body == null)
                throw new ArgumentNullException(nameof(body));

            if (!IsOpen)
                throw new DapException("Not connected to the debug adapter.");

            _outgoing.TryAdd(
                MessageFactory.Events.Create(eventType, body)
            );
        }

        /// <summary>
        ///     Send a request to the debug adapter.
        /// </summary>
        /// <param name="command">
        ///     The request command name.
        /// </param>
        /// <param name="arguments">
        ///     The request arguments.
        /// </param>
        /// <param name="cancellationToken">
        ///     An optional cancellation token that can be used to cancel the request.
        /// </param>
        /// <returns>
        ///     A <see cref="Task"/> representing the request.
        /// </returns>
        public async Task SendRequest(string command, object arguments, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(command))
                throw new ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: {nameof(command)}.", nameof(command));

            if (!IsOpen)
                throw new DapException("Not connected to the debug adapter.");

            DapRequest request = MessageFactory.Requests.Create(command, arguments);


            var responseCompletion = new TaskCompletionSource<DapResponse>(state: request.Id);
            cancellationToken.Register(() =>
            {
                responseCompletion.TrySetException(
                    new OperationCanceledException("The request was canceled via the supplied cancellation token.", cancellationToken)
                );

                // Send notification telling server to cancel the request, if possible.
                if (!_outgoing.IsAddingCompleted)
                {
                    _outgoing.TryAdd(
                        MessageFactory.Requests.Cancel(request.Id)
                    );
                }
            });

            _responseCompletions.TryAdd(request.Id, responseCompletion);

            _outgoing.TryAdd(request);

            await responseCompletion.Task;
        }

        /// <summary>
        ///     Send a request to the debug adapter.
        /// </summary>
        /// <typeparam name="TResponse">
        ///     The response message type.
        /// </typeparam>
        /// <param name="command">
        ///     The request command name.
        /// </param>
        /// <param name="arguments">
        ///     The request message.
        /// </param>
        /// <param name="cancellationToken">
        ///     An optional cancellation token that can be used to cancel the request.
        /// </param>
        /// <returns>
        ///     A <see cref="Task{TResult}"/> representing the response.
        /// </returns>
        public async Task<TResponse> SendRequest<TResponse>(string command, object arguments, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(command))
                throw new ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: {nameof(command)}.", nameof(command));

            if (arguments == null)
                throw new ArgumentNullException(nameof(arguments));

            if (!IsOpen)
                throw new DapException("Not connected to the debug adapter.");

            DapRequest request = MessageFactory.Requests.Create(command, arguments);

            var responseCompletion = new TaskCompletionSource<DapResponse>(state: request.Id);
            cancellationToken.Register(() =>
            {
                responseCompletion.TrySetException(
                    new OperationCanceledException("The request was canceled via the supplied cancellation token.", cancellationToken)
                );

                // Send notification telling server to cancel the request, if possible.
                if (!_outgoing.IsAddingCompleted)
                {
                    _outgoing.TryAdd(
                        MessageFactory.Requests.Cancel(
                            requestId: request.Id
                        )
                    );
                }
            });

            _responseCompletions.TryAdd(request.Id, responseCompletion);

            _outgoing.TryAdd(request);

            DapResponse response = await responseCompletion.Task;

            if (response.Body != null)
                return response.Body.ToObject<TResponse>(Serializer.JsonSerializer);
            else
                return default;
        }

        /// <summary>
        ///     The connection's message-send loop.
        /// </summary>
        /// <returns>
        ///     A <see cref="Task"/> representing the loop's activity.
        /// </returns>
        async Task SendLoop()
        {
            await Task.Yield();

            Log.LogInformation("Send loop started.");

            try
            {
                while (_outgoing.TryTake(out DapMessage outgoing, -1, _cancellation))
                {
                    try
                    {
                        switch (outgoing)
                        {
                            case DapRequest request:
                            {
                                Log.LogDebug("Sending outgoing {RequestCommand} request {RequestId}...", request.Command, request.Id);

                                await SendMessage(request);

                                Log.LogDebug("Sent outgoing {RequestCommand} request {RequestId}.", request.Command, request.Id);

                                break;
                            }
                            case DapEvent @event:
                            {
                                Log.LogDebug("Sending outgoing {EventType} event...", @event.Event);

                                await SendMessage(@event);

                                Log.LogDebug("Sent outgoing {EventType} event.", @event.Event);

                                break;
                            }
                            default:
                            {
                                Log.LogError("Unexpected outgoing message type '{0}'.", outgoing.GetType().AssemblyQualifiedName);

                                break;
                            }
                        }
                    }
                    catch (Exception sendError)
                    {
                        Log.LogError(sendError, "Unexpected error sending outgoing message {@Message}.", outgoing);
                    }
                }
            }
            catch (OperationCanceledException operationCanceled)
            {
                // Like tears in rain
                if (operationCanceled.CancellationToken != _cancellation)
                    throw; // time to die
            }
            finally
            {
                Log.LogInformation("Send loop terminated.");
            }
        }

        /// <summary>
        ///     The connection's message-receive loop.
        /// </summary>
        /// <returns>
        ///     A <see cref="Task"/> representing the loop's activity.
        /// </returns>
        async Task ReceiveLoop()
        {
            await Task.Yield();

            Log.LogInformation("Receive loop started.");

            try
            {
                while (!_cancellation.IsCancellationRequested && !_incoming.IsAddingCompleted)
                {
                    DapMessage message = await ReceiveMessage();
                    if (message == null)
                        continue;

                    _cancellation.ThrowIfCancellationRequested();

                    try
                    {
                        switch (message)
                        {
                            case DapRequest request:
                            {
                                Log.LogDebug("Received {RequestCommand} request {RequestId} from debug adapter: {RequestParameters}",
                                    request.Command,
                                    request.Id,
                                    request.Arguments?.ToString(Formatting.None)
                                );

                                // Publish.
                                if ( !_incoming.IsAddingCompleted )
                                    _incoming.TryAdd(message);

                                break;
                            }
                            case DapEvent @event:
                            {
                                Log.LogDebug("Received {EventType} event from debug adapter: {EventBody}",
                                    @event.Event,
                                    @event.Body?.ToString(Formatting.None)
                                );

                                // Publish.
                                if ( !_incoming.IsAddingCompleted )
                                    _incoming.TryAdd(message);

                                break;
                            }
                            case DapResponse response:
                            {
                                if ( _responseCompletions.TryGetValue(response.RequestId, out var completion) )
                                {
                                    if (!response.Success)
                                    {
                                        Log.LogDebug("Received error response {RequestId} from debug adapter: {ErrorMessage}\n{@ErrorBody}",
                                            response.RequestId,
                                            response.Message,
                                            response.Body
                                        );

                                        Log.LogDebug("Faulting request {RequestId}.", response.RequestId);

                                        completion.TrySetException(
                                            CreateDapException(response)
                                        );
                                    }
                                    else
                                    {
                                        Log.LogDebug("Received response {RequestId} from debug adapter: {ResponseResult}",
                                            response.RequestId,
                                            response.Body?.ToString(Formatting.None)
                                        );

                                        Log.LogDebug("Completing request {RequestId}.", response.RequestId);

                                        completion.TrySetResult(response);
                                    }
                                }
                                else
                                {
                                    Log.LogDebug("Received unexpected response {RequestId} from debug adapter: {ResponseResult}",
                                        response.RequestId,
                                        response.Body?.ToString(Formatting.None)
                                    );
                                }

                                break;
                            }
                        }
                    }
                    catch (Exception dispatchError)
                    {
                        Log.LogError(dispatchError, "Unexpected error processing incoming message {@Message}.", message);
                    }
                }
            }
            catch (OperationCanceledException operationCanceled)
            {
                // Like tears in rain
                if (operationCanceled.CancellationToken != _cancellation)
                    throw; // time to die
            }
            finally
            {
                Log.LogInformation("Receive loop terminated.");
            }
        }

        /// <summary>
        ///     Send a message to the debug adapter.
        /// </summary>
        /// <typeparam name="TMessage">
        ///     The type of message to send.
        /// </typeparam>
        /// <param name="message">
        ///     The message to send.
        /// </param>
        /// <returns>
        ///     A <see cref="Task"/> representing the operation.
        /// </returns>
        async Task SendMessage<TMessage>(TMessage message)
            where TMessage : class
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            string payload = JsonConvert.SerializeObject(message, Serializer.Settings);
            byte[] payloadBuffer = PayloadEncoding.GetBytes(payload);

            byte[] headerBuffer = HeaderEncoding.GetBytes(
                $"Content-Length: {payloadBuffer.Length}\r\n\r\n"
            );

            Log.LogDebug("Sending outgoing header ({HeaderSize} bytes)...", headerBuffer.Length);
            await _output.WriteAsync(headerBuffer, 0, headerBuffer.Length, _cancellation);
            Log.LogDebug("Sent outgoing header ({HeaderSize} bytes).", headerBuffer.Length);

            Log.LogDebug("Sending outgoing payload ({PayloadSize} bytes)...", payloadBuffer.Length);
            await _output.WriteAsync(payloadBuffer, 0, payloadBuffer.Length, _cancellation);
            Log.LogDebug("Sent outgoing payload ({PayloadSize} bytes).", payloadBuffer.Length);

            Log.LogDebug("Flushing output stream...");
            await _output.FlushAsync(_cancellation);
            Log.LogDebug("Flushed output stream.");
        }

        /// <summary>
        ///     Receive a message from the debug adapter.
        /// </summary>
        /// <returns>
        ///     A <see cref="DapMessage"/> representing the message,
        /// </returns>
        async Task<DapMessage> ReceiveMessage()
        {
            Log.LogDebug("Reading response headers...");

            byte[] headerBuffer = new byte[HeaderBufferSize];
            int bytesRead = await _input.ReadAsync(headerBuffer, 0, MinimumHeaderLength, _cancellation);

            Log.LogDebug("Read {ByteCount} bytes from input stream.", bytesRead);

            if (bytesRead == 0)
                return null; // Stream closed.

            const byte CR = (byte)'\r';
            const byte LF = (byte)'\n';

            while (bytesRead < MinimumHeaderLength ||
                   headerBuffer[bytesRead - 4] != CR || headerBuffer[bytesRead - 3] != LF ||
                   headerBuffer[bytesRead - 2] != CR || headerBuffer[bytesRead - 1] != LF)
            {
                Log.LogDebug("Reading additional data from input stream...");

                // Read single bytes until we've got a valid end-of-header sequence.
                var additionalBytesRead = await _input.ReadAsync(headerBuffer, bytesRead, 1, _cancellation);
                if (additionalBytesRead == 0)
                    return null; // no more _input, mitigates endless loop here.

                Log.LogDebug("Read {ByteCount} bytes of additional data from input stream.", additionalBytesRead);

                bytesRead += additionalBytesRead;
            }

            string headers = HeaderEncoding.GetString(headerBuffer, 0, bytesRead);
            Log.LogDebug("Got raw headers: {Headers}", headers);

            if (string.IsNullOrWhiteSpace(headers))
                return null; // Stream closed.

            Log.LogDebug("Read response headers {Headers}.", headers);

            Dictionary<string, string> parsedHeaders = ParseHeaders(headers);

            if (!parsedHeaders.TryGetValue("Content-Length", out var contentLengthHeader))
            {
                Log.LogDebug("Invalid request headers (missing 'Content-Length' header).");

                return null;
            }

            var contentLength = int.Parse(contentLengthHeader);

            Log.LogDebug("Reading response body ({ExpectedByteCount} bytes expected).", contentLength);

            var requestBuffer = new byte[contentLength];
            var received = 0;
            while (received < contentLength)
            {
                Log.LogDebug("Reading segment of incoming request body ({ReceivedByteCount} of {TotalByteCount} bytes so far)...", received, contentLength);

                var payloadBytesRead = await _input.ReadAsync(requestBuffer, received, requestBuffer.Length - received, _cancellation);
                if (payloadBytesRead == 0)
                {
                    Log.LogWarning("Bailing out of reading payload (no_more_input after {ByteCount} bytes)...", received);

                    return null;
                }
                received += payloadBytesRead;

                Log.LogDebug("Read segment of incoming request body ({ReceivedByteCount} of {TotalByteCount} bytes so far).", received, contentLength);
            }

            Log.LogDebug("Received entire payload ({ReceivedByteCount} bytes).", received);

            if (Log.IsEnabled(LogLevel.Debug))
            {
                Log.LogDebug("Read message body {MessageBody}.",
                    PayloadEncoding.GetString(requestBuffer)
                );
            }


            JObject messageBodyJson;
            using (Stream messageBodyStream = new MemoryStream(requestBuffer))
            using (TextReader messageBodyTextReader = new StreamReader(messageBodyStream, PayloadEncoding))
            using (JsonReader messageBodyJsonReader = new JsonTextReader(messageBodyTextReader))
            {
                messageBodyJson = JObject.Load(messageBodyJsonReader);
            }

            string rawMessageType = messageBodyJson.Value<string>("type");

            if ( !Enum.TryParse(rawMessageType, out DapMessageType messageType) )
            {
                Log.LogError("Unable to parse message ('type' property has unexpected value '{RawMessageType}').", rawMessageType);

                return null;
            }

            DapMessage message;

            switch (messageType)
            {
                case DapMessageType.Request:
                {
                    message = new DapRequest();

                    break;
                }
                case DapMessageType.Event:
                {
                    message = new DapEvent();

                    break;
                }
                case DapMessageType.Response:
                {
                    message = new DapResponse();

                    break;
                }
                default:
                {
                    Log.LogError("Unable to parse message ('type' property has unexpected value '{RawMessageType}').", messageType);

                    return null;
                }
            }

            using (JsonReader messageBodyReader = messageBodyJson.CreateReader())
            {
                Serializer.JsonSerializer.Populate(messageBodyReader, message);
            }

            return message;
        }

        /// <summary>
        ///     Parse request headers.
        /// </summary>
        /// <param name="rawHeaders">
        /// </param>
        /// <returns>
        ///     A <see cref="Dictionary{TKey, TValue}"/> containing the header names and values.
        /// </returns>
        private Dictionary<string, string> ParseHeaders(string rawHeaders)
        {
            if (rawHeaders == null)
                throw new ArgumentNullException(nameof(rawHeaders));

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); // Header names are case-insensitive.
            var rawHeaderEntries = rawHeaders.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string rawHeaderEntry in rawHeaderEntries)
            {
                string[] nameAndValue = rawHeaderEntry.Split(new char[] { ':' }, count: 2);
                if (nameAndValue.Length != 2)
                    continue;

                headers[nameAndValue[0].Trim()] = nameAndValue[1].Trim();
            }

            return headers;
        }

        /// <summary>
        ///     The connection's message-dispatch loop.
        /// </summary>
        /// <returns>
        ///     A <see cref="Task"/> representing the loop's activity.
        /// </returns>
        async Task DispatchLoop()
        {
            await Task.Yield();

            Log.LogInformation("Dispatch loop started.");

            try
            {
                while (_incoming.TryTake(out DapMessage message, -1, _cancellation))
                {
                    switch (message)
                    {
                        case DapRequest request:
                        {
                            if ( request.Command == "cancel" )
                                CancelRequest(request);
                            else
                                DispatchRequest(request);

                            break;
                        }
                        case DapEvent @event:
                        {
                            DispatchNotification(@event);

                            break;
                        }
                    }
                }
            }
            catch (OperationCanceledException operationCanceled)
            {
                // Like tears in rain
                if (operationCanceled.CancellationToken != _cancellation)
                    throw; // time to die
            }
            finally
            {
                Log.LogInformation("Dispatch loop terminated.");
            }
        }

        /// <summary>
        ///     Dispatch a request.
        /// </summary>
        /// <param name="requestMessage">
        ///     The request message.
        /// </param>
        private void DispatchRequest(DapRequest requestMessage)
        {
            if (requestMessage == null)
                throw new ArgumentNullException(nameof(requestMessage));

            Log.LogDebug("Dispatching incoming {RequestCommand} request {RequestId}...", requestMessage.Command, requestMessage.Id);

            var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(_cancellation);
            _requestCancellations.TryAdd(requestMessage.Id, requestCancellation);

            Task<object> handlerTask = _dispatcher.TryHandleRequest(requestMessage.Command, requestMessage.Arguments, requestCancellation.Token);
            if (handlerTask == null)
            {
                Log.LogWarning("Unable to dispatch incoming {RequestCommand} request {RequestId} (no handler registered).", requestMessage.Command, requestMessage.Id);

                _outgoing.TryAdd(
                    MessageFactory.Responses.Error.CommandNotFound(requestMessage)
                );

                return;
            }

#pragma warning disable CS4014 // Continuation does the work we need; no need to await it as this would tie up the dispatch loop.
            handlerTask.ContinueWith(_ =>
            {
                if (handlerTask.IsCanceled)
                    Log.LogDebug("{RequestCommand} request {RequestId} canceled.", requestMessage.Command, requestMessage.Id);
                else if (handlerTask.IsFaulted)
                {
                    Exception handlerError = handlerTask.Exception.Flatten().InnerExceptions[0];

                    Log.LogError(handlerError, "{RequestCommand} request {RequestId} failed (unexpected error raised by handler).", requestMessage.Command, requestMessage.Id);

                    _outgoing.TryAdd(
                        MessageFactory.Responses.Error.HandlerError(requestMessage, handlerError)
                    );
                }
                else if (handlerTask.IsCompleted)
                {
                    Log.LogDebug("{RequestCommand} request {RequestId} complete (Result = {@Result}).", requestMessage.Command, requestMessage.Id, handlerTask.Result);

                    _outgoing.TryAdd(
                        MessageFactory.Responses.Create(requestMessage, handlerTask.Result)
                    );
                }

                _requestCancellations.TryRemove(requestMessage.Id, out CancellationTokenSource cancellation);
                cancellation.Dispose();
            });
#pragma warning restore CS4014 // Continuation does the work we need; no need to await it as this would tie up the dispatch loop.

            Log.LogDebug("Dispatched incoming {RequestCommand} request {RequestId}.", requestMessage.Command, requestMessage.Id);
        }

        /// <summary>
        ///     Cancel a request.
        /// </summary>
        /// <param name="requestMessage">
        ///     The request message.
        /// </param>
        void CancelRequest(DapRequest requestMessage)
        {
            if (requestMessage == null)
                throw new ArgumentNullException(nameof(requestMessage));

            int? cancelRequestId = requestMessage.Arguments?.Value<int?>("id");
            if (cancelRequestId != null)
            {
                if (_requestCancellations.TryRemove(cancelRequestId.Value, out CancellationTokenSource requestCancellation))
                {
                    Log.LogDebug("Cancel request {RequestId}", requestMessage.Id);
                    requestCancellation.Cancel();
                    requestCancellation.Dispose();
                }
                else
                    Log.LogDebug("Received cancellation message for non-existent (or already-completed) request ");
            }
            else
            {
                Log.LogWarning("Received invalid request cancellation message {MessageId} (missing 'id' parameter).", requestMessage.Id);

                _outgoing.TryAdd(
                    MessageFactory.Responses.Error.InvalidArguments(requestMessage)
                );
            }
        }

        /// <summary>
        ///     Dispatch a notification.
        /// </summary>
        /// <param name="eventMessage">
        ///     The notification message.
        /// </param>
        void DispatchNotification(DapEvent eventMessage)
        {
            if (eventMessage == null)
                throw new ArgumentNullException(nameof(eventMessage));

            Log.LogDebug("Dispatching incoming {EventType} notification...", eventMessage.Event);

            Task<bool> handlerTask;
            if (eventMessage.Body != null)
                handlerTask = _dispatcher.TryHandleNotification(eventMessage.Event, eventMessage.Body);
            else
                handlerTask = _dispatcher.TryHandleEmptyNotification(eventMessage.Event);

#pragma warning disable CS4014 // Continuation does the work we need; no need to await it as this would tie up the dispatch loop.
            handlerTask.ContinueWith(completedHandler =>
            {
                if (handlerTask.IsCanceled)
                    Log.LogDebug("{EventType} notification canceled.", eventMessage.Event);
                else if (handlerTask.IsFaulted)
                {
                    Exception handlerError = handlerTask.Exception.Flatten().InnerExceptions[0];

                    Log.LogError(handlerError, "Failed to dispatch {EventType} notification (unexpected error raised by handler).", eventMessage.Event);
                }
                else if (handlerTask.IsCompleted)
                {
                    Log.LogDebug("{EventType} notification complete.", eventMessage.Event);

                    if (completedHandler.Result)
                        Log.LogDebug("Dispatched incoming {EventType} notification.", eventMessage.Event);
                    else
                        Log.LogDebug("Ignored incoming {EventType} notification (no handler registered).", eventMessage.Event);
                }
            });
#pragma warning restore CS4014 // Continuation does the work we need; no need to await it as this would tie up the dispatch loop.
        }

        /// <summary>
        ///     Create an <see cref="DapException"/> to represent the specified message.
        /// </summary>
        /// <param name="response">
        ///     The <see cref="DapMessage"/> (<see cref="DapMessage.Error"/> must be populated).
        /// </param>
        /// <returns>
        ///     The new <see cref="DapException"/>.
        /// </returns>
        static DapException CreateDapException(DapResponse response)
        {
            if (response == null)
                throw new ArgumentNullException(nameof(response));

            Trace.Assert(response.Body != null, "message.Body != null");

            int errorCode = DapErrorCodes.None;
            if (response.Body is JObject body)
            {
                errorCode = body.Value<int?>("code") ?? DapErrorCodes.None;
            }

            switch (errorCode)
            {
                case DapErrorCodes.InvalidRequest:
                {
                    return new DapInvalidRequestException(response.RequestId);
                }
                case DapErrorCodes.InvalidArguments:
                {
                    return new DapInvalidParametersException(response.RequestId);
                }
                case DapErrorCodes.InternalError:
                {
                    return new DapInternalErrorException(response.RequestId);
                }
                case DapErrorCodes.CommandNotFound:
                {
                    return new DapCommandNotFoundException(response.Command, response.RequestId);
                }
                case DapErrorCodes.RequestCancelled:
                {
                    return new DapRequestCancelledException(response.RequestId);
                }
                default:
                {
                    string exceptionMessage = $"Error processing request '{response.Id}' ({errorCode}): {response.Message}";

                    return new DapRequestException(exceptionMessage, response.RequestId, errorCode);
                }
            }
        }
    }
}
