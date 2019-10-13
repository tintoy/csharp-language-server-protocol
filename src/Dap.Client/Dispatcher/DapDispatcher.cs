using System;
using System.Collections.Concurrent;
using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.DebugAdapter.Client.Handlers;

namespace OmniSharp.Extensions.DebugAdapter.Client.Dispatcher
{
    /// <summary>
    ///     Dispatches incoming requests and events from a debug adapter to a client.
    /// </summary>
    public class DapDispatcher
    {
        /// <summary>
        ///     Invokers for registered handlers.
        /// </summary>
        readonly ConcurrentDictionary<string, IHandler> _handlers = new ConcurrentDictionary<string, IHandler>();

        /// <summary>
        ///     Create a new <see cref="DapDispatcher"/>.
        /// </summary>
        /// <param name="serializer">
        ///     The JSON serialiser for event / request / response payloads.
        /// </param>
        public DapDispatcher(ISerializer serializer)
        {
            if (serializer == null)
                throw new ArgumentNullException(nameof(serializer));

            Serializer = serializer;
        }

        /// <summary>
        ///     The JSON serialiser to use for event / request / response payloads.
        /// </summary>
        public ISerializer Serializer { get; set; }

        /// <summary>
        ///     Register a handler invoker.
        /// </summary>
        /// <param name="handler">
        ///     The handler.
        /// </param>
        /// <returns>
        ///     An <see cref="IDisposable"/> representing the registration.
        /// </returns>
        public IDisposable RegisterHandler(IHandler handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            string method = handler.Method;

            if (!_handlers.TryAdd(method, handler))
                throw new InvalidOperationException($"There is already a handler registered for method '{handler.Method}'.");

            return Disposable.Create(
                () => _handlers.TryRemove(method, out _)
            );
        }

        /// <summary>
        ///     Attempt to handle an empty event.
        /// </summary>
        /// <param name="eventType">
        ///     The event type (name).
        /// </param>
        /// <returns>
        ///     <c>true</c>, if an empty event handler was registered for specified method; otherwise, <c>false</c>.
        /// </returns>
        public async Task<bool> TryHandleEmptyEvent(string eventType)
        {
            if (string.IsNullOrWhiteSpace(eventType))
                throw new ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: {nameof(eventType)}.", nameof(eventType));

            if (_handlers.TryGetValue(eventType, out IHandler handler) && handler is IInvokeDapEmptyEventHandler emptyEventHandler)
            {
                await emptyEventHandler.Invoke();

                return true;
            }

            return false;
        }

        /// <summary>
        ///     Attempt to handle an event.
        /// </summary>
        /// <param name="eventType">
        ///     The event type (name).
        /// </param>
        /// <param name="body">
        ///     The event body.
        /// </param>
        /// <returns>
        ///     <c>true</c>, if an event handler was registered for specified method; otherwise, <c>false</c>.
        /// </returns>
        public async Task<bool> TryHandleEvent(string eventType, JToken body)
        {
            if (string.IsNullOrWhiteSpace(eventType))
                throw new ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: {nameof(eventType)}.", nameof(eventType));

            if (_handlers.TryGetValue(eventType, out IHandler handler) && handler is IInvokeDapEventHandler eventHandler)
            {
                object eventPayload = DeserializePayload(eventHandler.PayloadType, body);

                await eventHandler.Invoke(eventPayload);

                return true;
            }

            return false;
        }

        /// <summary>
        ///     Attempt to handle a request.
        /// </summary>
        /// <param name="command">
        ///     The request command name.
        /// </param>
        /// <param name="arguments">
        ///     The request arguments.
        /// </param>
        /// <param name="cancellationToken">
        ///     A <see cref="CancellationToken"/> that can be used to cancel the operation.
        /// </param>
        /// <returns>
        ///     If a registered handler was found, a <see cref="Task"/> representing the operation; otherwise, <c>null</c>.
        /// </returns>
        public Task<object> TryHandleRequest(string command, JToken arguments, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(command))
                throw new ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: {nameof(command)}.", nameof(command));

            if (_handlers.TryGetValue(command, out IHandler handler) && handler is IInvokeDapRequestHandler requestHandler)
            {
                object requestPayload = DeserializePayload(requestHandler.PayloadType, arguments);

                return requestHandler.Invoke(requestPayload, cancellationToken);
            }

            return null;
        }

        /// <summary>
        ///     Deserialise an event / request payload from JSON.
        /// </summary>
        /// <param name="payloadType">
        ///     The payload's CLR type.
        /// </param>
        /// <param name="payload">
        ///     JSON representing the payload.
        /// </param>
        /// <returns>
        ///     The deserialised payload (if one is present and expected).
        /// </returns>
        object DeserializePayload(Type payloadType, JToken payload)
        {
            if (payloadType == null)
                throw new ArgumentNullException(nameof(payloadType));

            if (payloadType == null || payload == null)
                return null;

            return payload.ToObject(payloadType, Serializer.JsonSerializer);
        }
    }
}
