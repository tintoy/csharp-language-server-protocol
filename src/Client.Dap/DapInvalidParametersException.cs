using System;
using System.Runtime.Serialization;

namespace OmniSharp.Extensions.DebugAdapter.Client
{
    /// <summary>
    ///     Exception raised when request parameters are invalid according to the target method.
    /// </summary>
    [Serializable]
    public class DapInvalidParametersException
        : DapRequestException
    {
        /// <summary>
        ///     Create a new <see cref="DapInvalidParametersException"/>.
        /// </summary>
        /// <param name="requestId">
        ///     The DAP / JSON-RPC request Id (if known).
        /// </param>
        public DapInvalidParametersException(int? requestId)
            : base("Invalid arguments.", requestId, DapErrorCodes.InvalidArguments)
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
        protected DapInvalidParametersException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
