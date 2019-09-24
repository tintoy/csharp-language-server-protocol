using System;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol;

namespace OmniSharp.Extensions.LanguageServer.Client.Clients
{
    /// <summary>
    ///     Client for the LSP Workspace API.
    /// </summary>
    public class WorkspaceClient
        : LspClientBase
    {
        /// <summary>
        ///     Create a new <see cref="WorkspaceClient"/>.
        /// </summary>
        /// <param name="client">
        ///     The language client providing the API.
        /// </param>
        public WorkspaceClient(LanguageClient client)
            : base(client)
        {
        }

        /// <summary>
        ///     Notify the language server that workspace configuration has changed.
        /// </summary>
        /// <param name="configuration">
        ///     A <see cref="JObject"/> representing the workspace configuration (or a subset thereof).
        /// </param>
        public void DidChangeConfiguration(JObject configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            Client.SendNotification(WorkspaceNames.DidChangeConfiguration, new JObject(
                new JProperty("settings", configuration)
            ));
        }
    }
}
