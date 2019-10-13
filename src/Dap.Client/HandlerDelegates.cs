using System.Threading;
using System.Threading.Tasks;

namespace OmniSharp.Extensions.DebugAdapter.Client
{
    /// <summary>
    ///     A handler for Debug Adapter events.
    /// </summary>
    public delegate void DapEventHandler();

    /// <summary>
    ///     A handler for Debug Adapter events.
    /// </summary>
    /// <typeparam name="TEvent">
    ///     The event body type.
    /// </typeparam>
    /// <param name="body">
    ///     The event body.
    /// </param>
    public delegate void DapEventHandler<TEvent>(TEvent body);

    /// <summary>
    ///     A handler for requests that return responses.
    /// </summary>
    /// <typeparam name="TRequest">
    ///     The request body type.
    /// </typeparam>
    /// <typeparam name="TResponse">
    ///     The response body type.
    /// </typeparam>
    /// <param name="requestBody">
    ///     The request body.
    /// </param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> that can be used to cancel the operation.
    /// </param>
    /// <returns>
    ///     A <see cref="Task{TResult}"/> representing the operation that resolves to the response message.
    /// </returns>
    public delegate Task<TResponse> DapRequestHandler<TRequest, TResponse>(TRequest requestBody, CancellationToken cancellationToken);
}
