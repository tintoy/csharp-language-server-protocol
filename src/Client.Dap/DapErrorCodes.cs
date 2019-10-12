using System;
using System.Collections.Generic;
using System.Text;

namespace OmniSharp.Extensions.DebugAdapter.Client
{
    /// <summary>
    ///     Well-known DAP error codes.
    /// </summary>
    public static class DapErrorCodes
    {
        /// <summary>
        ///     No error code was supplied.
        /// </summary>
        public static readonly int None = -32001;

        /// <summary>
        ///     Server has not been initialised.
        /// </summary>
        public const int ServerNotInitialized = -32002;

        /// <summary>
        ///     Method not found.
        /// </summary>
        public const int CommandNotFound = -32601;

        /// <summary>
        ///     Invalid request.
        /// </summary>
        public const int InvalidRequest = -32600;

        /// <summary>
        ///     Invalid request parameters.
        /// </summary>
        public const int InvalidArguments = -32602;

        /// <summary>
        ///     Internal error.
        /// </summary>
        public const int InternalError = -32603;

        /// <summary>
        ///     Unable to parse request.
        /// </summary>
        public const int ParseError = -32700;

        /// <summary>
        ///     Request was cancelled.
        /// </summary>
        public const int RequestCancelled = -32800;

        /// <summary>
        ///     Request was cancelled.
        /// </summary>
        public const int ContentModified = -32801;

        /// <summary>
        ///     Request hander encountered an unexpected error.
        /// </summary>
        public const int HandlerError = 500;
    }
}
