using System;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.DebugAdapter.Protocol.Requests;

namespace OmniSharp.Extensions.DebugAdapter.Client.Clients
{
    /// <summary>
    ///     Client for the process-related part of the Debug Adapter API.
    /// </summary>
    public class ProcessClient
        : DapClientBase
    {
        /// <summary>
        ///     Create a new <see cref="ProcessClient"/>.
        /// </summary>
        /// <param name="client">
        ///     The <see cref="DebugAdapterClient"/> used to communicate with the debug adapter.
        /// </param>
        public ProcessClient(DebugAdapterClient client)
            : base(client)
        {
        }

        /// <summary>
        ///     Instruct the debug adapter to attach to 
        /// </summary>
        /// <param name="arguments">
        ///     The request arguments (since the arguments required to attach to a process are adapter-specific, this is usually a type derived from <see cref="AttachRequestArguments"/>, rather than <see cref="AttachRequestArguments"/> itself).</param>
        /// <param name="cancellation">
        ///     An optional <see cref="CancellationToken"/> that can be used to cancel the request.
        /// </param>
        /// <returns>
        ///     An <see cref="AttachResponse"/> representing the response.
        /// </returns>
        public async Task<AttachResponse> Attach(AttachRequestArguments arguments, CancellationToken cancellation = default)
        {
            if ( arguments == null )
                throw new ArgumentNullException(nameof(arguments));

            return await Client.SendRequest<AttachResponse>("attach", arguments, cancellation).ConfigureAwait(false);
        }
    }
}
