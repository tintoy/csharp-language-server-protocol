using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.DebugAdapter.Protocol.Serialization;

namespace OmniSharp.Extensions.DebugAdapter.Client.Protocol
{
    /// <summary>
    ///     A factory for common Debug Adapter Protocol messages.
    /// </summary>
    public class DapMessageFactory
    {
        /// <summary>
        ///     The next available message Id.
        /// </summary>
        int _nextId = 0;

        /// <summary>
        ///     Create a new <see cref="DapMessageFactory"/>.
        /// </summary>
        /// <param name="serializer">
        ///     The serialiser to use for message payloads.
        /// </param>
        public DapMessageFactory(DapProtocolSerializer serializer)
        {
            if ( serializer == null )
                throw new ArgumentNullException(nameof(serializer));

            Serializer = serializer;

            Requests = new DapRequestMessageFactory(this);
            Events = new DapEventMessageFactory(this);
            Responses = new DapResponseMessageFactory(this);
        }

        /// <summary>
        ///     The serialiser to use for message payloads.
        /// </summary>
        internal DapProtocolSerializer Serializer { get; }

        /// <summary>
        ///     The factory for Debug Adapter Protocol request messages.
        /// </summary>
        public DapRequestMessageFactory Requests { get; }

        /// <summary>
        ///     The factory for Debug Adapter Protocol event messages.
        /// </summary>
        public DapEventMessageFactory Events { get; }

        /// <summary>
        ///     The factory for Debug Adapter Protocol response messages.
        /// </summary>
        public DapResponseMessageFactory Responses { get; }

        /// <summary>
        /// Get the next available message Id.
        /// </summary>
        /// <returns>The message Id.</returns>
        public int NextId() => Interlocked.Increment(ref _nextId);
    }

    /// <summary>
    ///     A factory for Debug Adapter Protocol request messages.
    /// </summary>
    public class DapRequestMessageFactory
    {
        /// <summary>
        ///     Create a new <see cref="DapRequestMessageFactory"/>.
        /// </summary>
        /// <param name="messageFactory">
        ///     The factory for message payloads.
        /// </param>
        public DapRequestMessageFactory(DapMessageFactory messageFactory)
        {
            if ( messageFactory == null )
                throw new ArgumentNullException(nameof(messageFactory));

            MessageFactory = messageFactory;
        }

        /// <summary>
        ///     The factory for message payloads.
        /// </summary>
        DapMessageFactory MessageFactory { get; }

        /// <summary>
        ///     Create a request cancellation message.
        /// </summary>
        /// <param name="requestId">The Id of the request to cancel.</param>
        /// <returns>The new <see cref="DapRequest"/>.</returns>
        public DapRequest Cancel(int requestId) => Create("cancel", new DapCancellationArguments { RequestId = requestId });

        /// <summary>
        ///     Create a new request message.
        /// </summary>
        /// <param name="requestId">
        ///     The request Id.
        /// </param>
        /// <param name="command">
        ///     The request command.
        /// </param>
        /// <param name="arguments">
        ///     The command arguments.
        /// </param>
        /// <returns>
        ///     The new <see cref="DapRequest"/>.
        /// </returns>
        public DapRequest Create(string command, object arguments)
        {
            if ( string.IsNullOrWhiteSpace(command) )
                throw new ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: {nameof(command)}.", nameof(command));

            return new DapRequest
            {
                Id = MessageFactory.NextId(),
                Command = command,
                Arguments = arguments != null ? JToken.FromObject(arguments, MessageFactory.Serializer.JsonSerializer) : null
            };
        }
    }

    /// <summary>
    ///     A factory for Debug Adapter Protocol event messages.
    /// </summary>
    public class DapEventMessageFactory
    {
        /// <summary>
        ///     Create a new <see cref="DapEventMessageFactory"/>.
        /// </summary>
        /// <param name="messageFactory">
        ///     The factory for message payloads.
        /// </param>
        public DapEventMessageFactory(DapMessageFactory messageFactory)
        {
            if ( messageFactory == null )
                throw new ArgumentNullException(nameof(messageFactory));

            MessageFactory = messageFactory;
        }

        /// <summary>
        ///     The factory for message payloads.
        /// </summary>
        DapMessageFactory MessageFactory { get; }

        /// <summary>
        ///     Create a new event message.
        /// </summary>
        /// <param name="eventId">
        ///     The event Id.
        /// </param>
        /// <param name="eventType">
        ///     The event type.
        /// </param>
        /// <param name="body">
        ///     The eventType arguments.
        /// </param>
        /// <returns>
        ///     The new <see cref="DapEvent"/>.
        /// </returns>
        public DapEvent Create(string eventType, object body = null)
        {
            if ( string.IsNullOrWhiteSpace(eventType) )
                throw new ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: {nameof(eventType)}.", nameof(eventType));

            return new DapEvent
            {
                Id = MessageFactory.NextId(),
                Event = eventType,
                Body = body != null ? JToken.FromObject(body, MessageFactory.Serializer.JsonSerializer) : null
            };
        }
    }

    /// <summary>
    ///     A factory for Debug Adapter Protocol response messages.
    /// </summary>
    public class DapResponseMessageFactory
    {
        /// <summary>
        ///     Create a new <see cref="DapResponseMessageFactory"/>.
        /// </summary>
        /// <param name="messageFactory">
        ///     The factory for message payloads.
        /// </param>
        public DapResponseMessageFactory(DapMessageFactory messageFactory)
        {
            if ( messageFactory == null )
                throw new ArgumentNullException(nameof(messageFactory));

            MessageFactory = messageFactory;
        }

        /// <summary>
        ///     The factory for message payloads.
        /// </summary>
        DapMessageFactory MessageFactory { get; }

        /// <summary>
        ///     The factory for Debug Adapter Protocol error-response messages.
        /// </summary>
        public DapErrorResponseFactory Error { get; }

        /// <summary>
        ///     Create a <see cref="DapResponse"/> representing a response from a handler.
        /// </summary>
        /// <param name="request">
        ///     A <see cref="DapRequest"/> representing the request that the response relates to.
        /// </param>
        /// <param name="body">
        ///     The response body (will be serialised) or <c>null</c> if the response has no body.
        /// </param>
        /// <returns>
        ///     The new <see cref="DapResponse"/>.
        /// </returns>
        public DapResponse Create(DapRequest request, object body)
        {
            if ( request == null )
                throw new ArgumentNullException(nameof(request));

            return new DapResponse
            {
                Id = MessageFactory.NextId(),
                RequestId = request.Id,
                Command = request.Command,
                Body = body != null ? JToken.FromObject(body, MessageFactory.Serializer.JsonSerializer) : null
            };
        }
    }

    /// <summary>
    ///     A factory for Debug Adapter Protocol error-response messages.
    /// </summary>
    public class DapErrorResponseFactory
    {
        /// <summary>
        ///     Create a new <see cref="DapErrorResponseFactory"/>.
        /// </summary>
        /// <param name="messageFactory">
        ///     The factory for message payloads.
        /// </param>
        public DapErrorResponseFactory(DapMessageFactory messageFactory)
        {
            if ( messageFactory == null )
                throw new ArgumentNullException(nameof(messageFactory));

            MessageFactory = messageFactory;
        }

        /// <summary>
        ///     The factory for message payloads.
        /// </summary>
        DapMessageFactory MessageFactory { get; }

        /// <summary>
        ///     Create a <see cref="DapResponse"/> representing <see cref="DapErrorCodes.CommandNotFound"/>.
        /// </summary>
        /// <param name="request">
        ///     A <see cref="DapRequest"/> representing the request that the response relates to.
        /// </param>
        /// <returns>
        ///     The new <see cref="DapResponse"/>.
        /// </returns>
        public DapResponse CommandNotFound(DapRequest request) => Create(request, $"Command not found: '{request?.Command}'", DapErrorCodes.CommandNotFound);

        /// <summary>
        ///     Create a <see cref="DapResponse"/> representing <see cref="DapErrorCodes.InvalidArguments"/>.
        /// </summary>
        /// <param name="request">
        ///     A <see cref="DapRequest"/> representing the request that the response relates to.
        /// </param>
        /// <returns>
        ///     The new <see cref="DapResponse"/>.
        /// </returns>
        public DapResponse InvalidArguments(DapRequest request) => Create(request, "Invalid arguments", DapErrorCodes.InvalidArguments);

        /// <summary>
        ///     Create a <see cref="DapResponse"/> representing <see cref="DapErrorCodes.HandlerError"/>.
        /// </summary>
        /// <param name="request">
        ///     A <see cref="DapRequest"/> representing the request that the response relates to.
        /// </param>
        /// <param name="errorCode">
        ///     The error code (defaults to <see cref="DapErrorCodes.HandlerError"/>).
        /// </param>
        /// <returns>
        ///     The new <see cref="DapResponse"/>.
        /// </returns>
        public DapResponse HandlerError(DapRequest request, Exception handlerError, int errorCode = DapErrorCodes.HandlerError)
        {
            if ( request == null )
                throw new ArgumentNullException(nameof(request));

            if ( handlerError == null )
                throw new ArgumentNullException(nameof(handlerError));

            return Create(request, $"Error processing request: {handlerError.Message}", errorCode,
                body => {
                    body.Add("data",
                        handlerError.ToString()
                    );
                }
            );
        }

        /// <summary>
        ///     Create a <see cref="DapResponse"/> representing an error response.
        /// </summary>
        /// <param name="request">
        ///     A <see cref="DapRequest"/> representing the request that the response relates to.
        /// </param>
        /// <param name="errorMessage">
        ///     The error message.
        /// </param>
        /// <param name="errorCode">
        ///     The error code.
        /// </param>
        /// <param name="configureBody">
        ///     An optional delegate that performs additional configuration of the response body.
        /// </param>
        /// <returns>
        ///     The new <see cref="DapResponse"/>.
        /// </returns>
        public DapResponse Create(DapRequest request, string errorMessage, int errorCode, Action<JObject> configureBody = null)
        {
            if ( request == null )
                throw new ArgumentNullException(nameof(request));

            if ( string.IsNullOrWhiteSpace(errorMessage) )
                throw new ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: {nameof(errorMessage)}.", nameof(errorMessage));

            var response = new DapResponse
            {
                Id = MessageFactory.NextId(),
                RequestId = request.Id,
                Command = request.Command,
                Message = errorMessage,
                Body = new JObject(
                    new JProperty("code", errorCode)
                )
            };

            if ( configureBody != null )
            {
                configureBody(
                    (JObject) response.Body
                );
            }

            return response;
        }
    }
}
