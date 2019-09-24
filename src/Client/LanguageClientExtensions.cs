using System;

namespace OmniSharp.Extensions.LanguageServer.Client
{
    using Clients;

    /// <summary>
    ///     Extension methods for <see cref="LanguageClient"/>.
    /// </summary>
    public static class LanguageClientExtensions
    {
        /// <summary>
        ///     The LSP Text Document API.
        /// </summary>
        /// <param name="client">
        ///     The language client providing the API.
        /// </param>
        /// <returns>
        ///     The <see cref="TextDocumentClient"/>.
        /// </returns>
        public static TextDocumentClient TextDocument(this LanguageClient client)
        {
            if ( client == null )
                throw new ArgumentNullException(nameof(client));

            return client.GetClient<TextDocumentClient>();
        }

        /// <summary>
        ///     The LSP Window API.
        /// </summary>
        /// <param name="client">
        ///     The language client providing the API.
        /// </param>
        /// <returns>
        ///     The <see cref="WindowClient"/>.
        /// </returns>
        public static WindowClient Window(this LanguageClient client)
        {
            if ( client == null )
                throw new ArgumentNullException(nameof(client));

            return client.GetClient<WindowClient>();
        }

        /// <summary>
        ///     Geth the client for the LSP Workspace API.
        /// </summary>
        /// <param name="client">
        ///     The language client providing the API.
        /// </param>
        /// <returns>
        ///     The <see cref="WorkspaceClient"/>.
        /// </returns>
        public static WorkspaceClient Workspace(this LanguageClient client)
        {
            if ( client == null )
                throw new ArgumentNullException(nameof(client));

            return client.GetClient<WorkspaceClient>();
        }
    }
}
