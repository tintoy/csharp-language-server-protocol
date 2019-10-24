using OmniSharp.Extensions.DebugAdapter.Client.Clients;

namespace OmniSharp.Extensions.DebugAdapter.Client
{
    /// <summary>
    ///     Extension methods for <see cref="DebugAdapterClient"/>.
    /// </summary>
    public static class DebugAdapterClientExtensions
    {
        /// <summary>
        ///     Get the client for the process-related part of the Debug Adapter API.
        /// </summary>
        /// <param name="client">
        ///     The <see cref="DebugAdapterClient"/> used to communicate with the debug adapter.
        /// </param>
        /// <returns>
        ///     The <see cref="ProcessClient"/>.
        /// </returns>
        public static ProcessClient Process(this DebugAdapterClient client) => client.GetClient<ProcessClient>();
    }
}
