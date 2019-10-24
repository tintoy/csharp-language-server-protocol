using System;
using Microsoft.Extensions.Logging;

namespace OmniSharp.Extensions.DebugAdapter.Client.Clients
{
    /// <summary>
    ///     The base class for Debug Adapter sub-clients.
    /// </summary>
    public abstract class DapClientBase
    {
        /// <summary>
        ///     Initialise <see cref="DapClientBase"/>.
        /// </summary>
        /// <param name="client">
        ///     The language client providing the API.
        /// </param>
        protected DapClientBase(DebugAdapterClient client)
        {
            if ( client == null )
                throw new ArgumentNullException(nameof(client));

            Client = client;
            Log = LoggerFactory.CreateLogger(GetType());
        }

        /// <summary>
        ///     The language client providing the API.
        /// </summary>
        public DebugAdapterClient Client { get; }

        /// <summary>
        ///     The factory for loggers used by the client and its components.
        /// </summary>
        protected ILoggerFactory LoggerFactory => Client.LoggerFactory;

        /// <summary>
        ///     The client's logger.
        /// </summary>
        protected ILogger Log { get; }
    }
}
