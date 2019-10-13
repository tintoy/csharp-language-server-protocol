using System;
using OmniSharp.Extensions.DebugAdapter.Client.Handlers;

namespace OmniSharp.Extensions.DebugAdapter.Client.Protocol
{
    /// <summary>
    ///     Extension methods for <see cref="DapConnection"/> enabling various styles of handler registration.
    /// </summary>
    public static class DapConnectionExtensions
    {
        /// <summary>
        ///     Register a handler for notifications.
        /// </summary>
        /// <typeparam name="TEvent">
        ///     The notification message type.
        /// </typeparam>
        /// <param name="clientConnection">
        ///     The <see cref="DapConnection"/>.
        /// </param>
        /// <param name="eventType">
        ///     The name of the notification method to handle.
        /// </param>
        /// <param name="handler">
        ///     A <see cref="EventHandler{TEvent}"/> delegate that implements the handler.
        /// </param>
        /// <returns>
        ///     An <see cref="IDisposable"/> representing the registration.
        /// </returns>
        public static IDisposable HandleEvent<TEvent>(this DapConnection clientConnection, string eventType, DapEventHandler<TEvent> handler)
        {
            if (clientConnection == null)
                throw new ArgumentNullException(nameof(clientConnection));

            if (string.IsNullOrWhiteSpace(eventType))
                throw new ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: {nameof(eventType)}.", nameof(eventType));

            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            return clientConnection.RegisterHandler(
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
        /// <param name="clientConnection">
        ///     The <see cref="DapConnection"/>.
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
        public static IDisposable HandleRequest<TRequest, TResponse>(this DapConnection clientConnection, string method, DapRequestHandler<TRequest, TResponse> handler)
        {
            if (clientConnection == null)
                throw new ArgumentNullException(nameof(clientConnection));

            if (string.IsNullOrWhiteSpace(method))
                throw new ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: {nameof(method)}.", nameof(method));

            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            return clientConnection.RegisterHandler(
                new DelegateRequestResponseHandler<TRequest, TResponse>(method, handler)
            );
        }
    }
}
