using System;
using System.Threading.Tasks;

namespace OmniSharp.Extensions.DebugAdapter.Client.Handlers
{
    /// <summary>
    ///     A delegate-based handler for events.
    /// </summary>
    /// <typeparam name="TEvent">
    ///     The event message type.
    /// </typeparam>
    public class DelegateNotificationHandler<TEvent>
        : DelegateHandler, IInvokeNotificationHandler
    {
        /// <summary>
        ///     Create a new <see cref="DelegateNotificationHandler{TNotification}"/>.
        /// </summary>
        /// <param name="method">
        ///     The name of the method handled by the handler.
        /// </param>
        /// <param name="handler">
        ///     The <see cref="NotificationHandler{TNotification}"/> delegate that implements the handler.
        /// </param>
        public DelegateNotificationHandler(string method, NotificationHandler<TEvent> handler)
            : base(method)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            Handler = handler;
        }

        /// <summary>
        ///     The <see cref="NotificationHandler{TNotification}"/> delegate that implements the handler.
        /// </summary>
        public NotificationHandler<TEvent> Handler { get; }

        /// <summary>
        ///     The expected CLR type of the event payload.
        /// </summary>
        public override Type PayloadType => typeof(TEvent);

        /// <summary>
        ///     Invoke the handler.
        /// </summary>
        /// <param name="event">
        ///     The event message.
        /// </param>
        /// <returns>
        ///     A <see cref="Task"/> representing the operation.
        /// </returns>
        public async Task Invoke(object @event)
        {
            await Task.Yield();

            Handler(
                (TEvent)@event
            );
        }
    }
}
