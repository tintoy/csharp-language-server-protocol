using System;
using System.Runtime.Serialization;

namespace OmniSharp.Extensions.DebugAdapter.Client
{
    /// <summary>
    ///     Exception raised when a Language Server Protocol error is encountered while processing a request.
    /// </summary>
    [Serializable]
    public class DapRequestException
        : DapException
    {
        /// <summary>
        ///     The request Id used when no valid request Id was supplied.
        /// </summary>
        public const int UnknownRequestId = -1;

        /// <summary>
        ///     Create a new <see cref="DapRequestException"/> without an error code (<see cref="DapErrorCodes.None"/>).
        /// </summary>
        /// <param name="message">
        ///     The exception message.
        /// </param>
        /// <param name="requestId">
        ///     The DAP / JSON-RPC request Id (if known).
        /// </param>
        public DapRequestException(string message, int? requestId)
            : this(message, requestId, DapErrorCodes.None)
        {
        }

        /// <summary>
        ///     Create a new <see cref="DapRequestException"/>.
        /// </summary>
        /// <param name="message">
        ///     The exception message.
        /// </param>
        /// <param name="requestId">
        ///     The DAP / JSON-RPC request Id (if known).
        /// </param>
        /// <param name="errorCode">
        ///     The DAP / JSON-RPC error code.
        /// </param>
        public DapRequestException(string message, int? requestId, int errorCode)
            : base(message)
        {
            RequestId = requestId ?? UnknownRequestId;
            ErrorCode = errorCode;
        }

        /// <summary>
        ///     Create a new <see cref="DapRequestException"/>.
        /// </summary>
        /// <param name="message">
        ///     The exception message.
        /// </param>
        /// <param name="requestId">
        ///     The DAP / JSON-RPC request Id (if known).
        /// </param>
        /// <param name="inner">
        ///     The exception that caused this exception to be raised.
        /// </param>
        public DapRequestException(string message, int? requestId, Exception inner)
            : this(message, requestId, DapErrorCodes.None, inner)
        {
        }

        /// <summary>
        ///     Create a new <see cref="DapRequestException"/>.
        /// </summary>
        /// <param name="message">
        ///     The exception message.
        /// </param>
        /// <param name="requestId">
        ///     The DAP / JSON-RPC request Id (if known).
        /// </param>
        /// <param name="errorCode">
        ///     The DAP / JSON-RPC error code.
        /// </param>
        /// <param name="inner">
        ///     The exception that caused this exception to be raised.
        /// </param>
        public DapRequestException(string message, int? requestId, int errorCode, Exception inner)
            : base(message, inner)
        {
            RequestId = requestId ?? UnknownRequestId;
            ErrorCode = errorCode;
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
        protected DapRequestException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            RequestId = info.GetInt32(nameof(RequestId));
            ErrorCode = info.GetInt32(nameof(ErrorCode));
        }

        /// <summary>
        ///     Get exception data for serialisation.
        /// </summary>
        /// <param name="info">
        ///     The serialisation data-store.
        /// </param>
        /// <param name="context">
        ///     The serialisation streaming context.
        /// </param>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);

            info.AddValue(nameof(RequestId), RequestId);
            info.AddValue(nameof(ErrorCode), ErrorCode);
        }

        /// <summary>
        ///     The DAP / JSON-RPC request Id (if known).
        /// </summary>
        public int RequestId { get; }

        /// <summary>
        ///     The DAP / JSON-RPC error code.
        /// </summary>
        public int ErrorCode { get; }

        /// <summary>
        ///     Does the <see cref="DapRequestException"/> represent an DAP / JSON-RPC protocol error?
        /// </summary>
        public bool IsProtocolError => ErrorCode != DapErrorCodes.None;
    }
}
