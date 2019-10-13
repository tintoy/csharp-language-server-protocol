using System;
using OmniSharp.Extensions.DebugAdapter.Client.Handlers;

namespace OmniSharp.Extensions.DebugAdapter.Client.Dispatcher
{
    /// <summary>
    ///     Extension methods for <see cref="DapDispatcher"/> enabling various styles of handler registration.
    /// </summary>
    public static class DapDispatcherExtensions
    {
        /// <summary>
        ///     Register a handler for events that have no body.
        /// </summary>
        /// <typeparam name="TEvent">
        ///     The event body type.
        /// </typeparam>
        /// <param name="clientDispatcher">
        ///     The <see cref="DapDispatcher"/>.
        /// </param>
        /// <param name="eventType">
        ///     The type (name) of the event to handle.
        /// </param>
        /// <param name="handler">
        ///     A <see cref="DapEventHandler{TEvent}"/> delegate that implements the handler.
        /// </param>
        /// <returns>
        ///     An <see cref="IDisposable"/> representing the registration.
        /// </returns>
        public static IDisposable HandleEvent(this DapDispatcher clientDispatcher, string eventType, DapEventHandler handler)
        {
            if ( clientDispatcher == null )
                throw new ArgumentNullException(nameof(clientDispatcher));

            if ( string.IsNullOrWhiteSpace(eventType) )
                throw new ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: {nameof(eventType)}.", nameof(eventType));

            if ( handler == null )
                throw new ArgumentNullException(nameof(handler));

            return clientDispatcher.RegisterHandler(
                new DelegateEmptyDapEventHandler(eventType, handler)
            );
        }

        /// <summary>
        ///     Register a handler for events.
        /// </summary>
        /// <typeparam name="TEvent">
        ///     The event body type.
        /// </typeparam>
        /// <param name="clientDispatcher">
        ///     The <see cref="DapDispatcher"/>.
        /// </param>
        /// <param name="eventType">
        ///     The type (name) of the event to handle.
        /// </param>
        /// <param name="handler">
        ///     A <see cref="DapEventHandler{TEvent}"/> delegate that implements the handler.
        /// </param>
        /// <returns>
        ///     An <see cref="IDisposable"/> representing the registration.
        /// </returns>
        public static IDisposable HandleEvent<TEvent>(this DapDispatcher clientDispatcher, string eventType, DapEventHandler<TEvent> handler)
        {
            if (clientDispatcher == null)
                throw new ArgumentNullException(nameof(clientDispatcher));

            if (string.IsNullOrWhiteSpace(eventType))
                throw new ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: {nameof(eventType)}.", nameof(eventType));

            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            return clientDispatcher.RegisterHandler(
                new DelegateDapEventHandler<TEvent>(eventType, handler)
            );
        }

        /// <summary>
        ///     Register a handler for requests.
        /// </summary>
        /// <typeparam name="TRequest">
        ///     The request message type.
        /// </typeparam>
        /// <typeparam name="TResponse">
        ///     The response message type.
        /// </typeparam>
        /// <param name="clientDispatcher">
        ///     The <see cref="DapDispatcher"/>.
        /// </param>
        /// <param name="method">
        ///     The name of the request method to handle.
        /// </param>
        /// <param name="handler">
        ///     A <see cref="RequestHandler{TRequest}"/> delegate that implements the handler.
        /// </param>
        /// <returns>
        ///     An <see cref="IDisposable"/> representing the registration.
        /// </returns>
        public static IDisposable HandleRequest<TRequest, TResponse>(this DapDispatcher clientDispatcher, string method, DapRequestHandler<TRequest, TResponse> handler)
        {
            if (clientDispatcher == null)
                throw new ArgumentNullException(nameof(clientDispatcher));

            if (string.IsNullOrWhiteSpace(method))
                throw new ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: {nameof(method)}.", nameof(method));

            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            return clientDispatcher.RegisterHandler(
                new DelegateRequestResponseHandler<TRequest, TResponse>(method, handler)
            );
        }
    }
}
