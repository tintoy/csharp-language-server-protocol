using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;

namespace OmniSharp.Extensions.LanguageServer.Client.Clients
{
    /// <summary>
    ///     The base class for LSP sub-clients.
    /// </summary>
    public abstract class LspClientBase
    {
        /// <summary>
        ///     Initialise <see cref="LspClientBase"/>.
        /// </summary>
        /// <param name="client">
        ///     The language client providing the API.
        /// </param>
        protected LspClientBase(LanguageClient client)
        {
            if ( client == null )
                throw new ArgumentNullException(nameof(client));

            Client = client;
            Log = LoggerFactory.CreateLogger(GetType());
        }

        /// <summary>
        ///     The language client providing the API.
        /// </summary>
        public LanguageClient Client { get; }

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
