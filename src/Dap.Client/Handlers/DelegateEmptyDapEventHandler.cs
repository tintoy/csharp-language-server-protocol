using System;
using System.Threading.Tasks;

namespace OmniSharp.Extensions.DebugAdapter.Client.Handlers
{
    /// <summary>
    ///     A delegate-based handler for empty notifications.
    /// </summary>
    public class DelegateEmptyDapEventHandler
        : DelegateHandler, IInvokeDapEmptyEventHandler
    {
        /// <summary>
        ///     Create a new <see cref="DelegateEmptyDapEventHandler"/>.
        /// </summary>
        /// <param name="method">
        ///     The name of the method handled by the handler.
        /// </param>
        /// <param name="handler">
        ///     The <see cref="DapEventHandler"/> delegate that implements the handler.
        /// </param>
        public DelegateEmptyDapEventHandler(string method, DapEventHandler handler)
            : base(method)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            Handler = handler;
        }

        /// <summary>
        ///     The <see cref="DapEventHandler"/> delegate that implements the handler.
        /// </summary>
        public DapEventHandler Handler { get; }

        /// <summary>
        ///     The expected CLR type of the notification payload (<c>null</c>, since the handler does not use the request payload).
        /// </summary>
        public override Type PayloadType => null;

        /// <summary>
        ///     Invoke the handler.
        /// </summary>
        /// <returns>
        ///     A <see cref="Task"/> representing the operation.
        /// </returns>
        public async Task Invoke()
        {
            await Task.Yield();

            Handler();
        }
    }
}
