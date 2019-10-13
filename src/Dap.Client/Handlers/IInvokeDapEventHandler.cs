using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace OmniSharp.Extensions.DebugAdapter.Client.Handlers
{
    /// <summary>
    ///     Represents a handler for Debug Adapter events.
    /// </summary>
    public interface IInvokeDapEventHandler
        : IHandler
    {
        /// <summary>
        ///     Invoke the handler.
        /// </summary>
        /// <param name="body">
        ///     The event body.
        /// </param>
        /// <returns>
        ///     A <see cref="Task"/> representing the operation.
        /// </returns>
        Task Invoke(object body);
    }
}
