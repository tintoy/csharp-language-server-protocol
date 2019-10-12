using System;
using System.Runtime.Serialization;

namespace OmniSharp.Extensions.DebugAdapter.Client
{
    /// <summary>
    ///     Exception raised when an internal error has occurred in the language server.
    /// </summary>
    [Serializable]
    public class DapInternalErrorException
        : DapRequestException
    {
        /// <summary>
        ///     Create a new <see cref="DapInternalErrorException"/>.
        /// </summary>
        /// <param name="requestId">
        ///     The DAP / JSON-RPC request Id (if known).
        /// </param>
        public DapInternalErrorException(int? requestId)
            : base("Internal error.", requestId, DapErrorCodes.InternalError)
        {
        }

        /// <summary>
        ///     Serialisation constructor.
        /// </summary>
        /// <param name="info">
        ///     The serialisation data-store.
        /// </param>
        /// <param name="context">
        ///     The serialisation streaming context.
        /// </param>
        protected DapInternalErrorException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
