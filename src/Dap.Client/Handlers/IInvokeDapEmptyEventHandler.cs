using System.Threading.Tasks;

namespace OmniSharp.Extensions.DebugAdapter.Client.Handlers
{
    /// <summary>
    ///     Represents a handler for Debug Adapter events with no body.
    /// </summary>
    public interface IInvokeDapEmptyEventHandler
        : IHandler
    {
        /// <summary>
        ///     Invoke the handler.
        /// </summary>
        /// <returns>
        ///     A <see cref="Task"/> representing the operation.
        /// </returns>
        Task Invoke();
    }
}
