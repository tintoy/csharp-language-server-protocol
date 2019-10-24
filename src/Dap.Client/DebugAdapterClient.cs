using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.DebugAdapter.Client.Dispatcher;
using OmniSharp.Extensions.DebugAdapter.Client.Handlers;
using OmniSharp.Extensions.DebugAdapter.Client.Processes;
using OmniSharp.Extensions.DebugAdapter.Client.Protocol;
using OmniSharp.Extensions.DebugAdapter.Protocol.Models;

using ISerializer = OmniSharp.Extensions.DebugAdapter.Protocol.Serialization.ISerializer;
using DapProtocolSerializer = OmniSharp.Extensions.DebugAdapter.Protocol.Serialization.DapProtocolSerializer;
using OmniSharp.Extensions.DebugAdapter.Client.Clients;
using OmniSharp.Extensions.DebugAdapter.Protocol.Requests;
using OmniSharp.Extensions.DebugAdapter.Protocol.Events;

namespace OmniSharp.Extensions.DebugAdapter.Client
{
    /// <summary>
    ///     A client for the Debug Adapter Protocol.
    /// </summary>
    /// <remarks>
    ///     Note - at this stage, a <see cref="DebugAdapterClient"/> cannot be reused once <see cref="Shutdown"/> has been called; instead, create a new one.
    /// </remarks>
    public sealed class DebugAdapterClient
        : IDisposable
    {
        /// <summary>
        ///     The CLR <see cref="Type"/> representing the <see cref="DebugAdapterClient"/> class.
        /// </summary>
        static readonly Type DebugAdapterClientType = typeof(DebugAdapterClient);

        /// <summary>
        ///     Sub-clients for various areas of DAP functionality, keyed by client <see cref="Type"/>.
        /// </summary>
        readonly ConcurrentDictionary<Type, DapClientBase> _clients = new ConcurrentDictionary<Type, DapClientBase>();

        /// <summary>
        ///     The serialiser for notification / request / response bodies.
        /// </summary>
        /// <remarks>
        ///     TODO: Make this injectable. And what does client version do - do we have to negotiate this?
        /// </remarks>
        readonly ISerializer _serializer = new DapProtocolSerializer();

        /// <summary>
        ///     The dispatcher for incoming requests, notifications, and responses.
        /// </summary>
        readonly DapDispatcher _dispatcher;

        /// <summary>
        ///     The debug adapter process.
        /// </summary>
        ServerProcess _process;

        /// <summary>
        ///     The underlying DAP connection to the debug adapter process.
        /// </summary>
        DapConnection _connection;

        /// <summary>
        ///     Completion source that callers can await to determine when the debug adapter is ready to use (i.e. initialised).
        /// </summary>
        TaskCompletionSource<object> _readyCompletion = new TaskCompletionSource<object>();

        /// <summary>
        ///     Create a new <see cref="DebugAdapterClient"/>.
        /// </summary>
        /// <param name="loggerFactory">
        ///     The factory for loggers used by the client and its components.
        /// </param>
        /// <param name="serverStartInfo">
        ///     A <see cref="ProcessStartInfo"/> describing how to start the server process.
        /// </param>
        public DebugAdapterClient(ILoggerFactory loggerFactory, ProcessStartInfo serverStartInfo)
            : this(loggerFactory, new StdioServerProcess(loggerFactory, serverStartInfo))
        {
        }

        /// <summary>
        ///     Create a new <see cref="DebugAdapterClient"/>.
        /// </summary>
        /// <param name="loggerFactory">
        ///     The factory for loggers used by the client and its components.
        /// </param>
        /// <param name="process">
        ///     A <see cref="ServerProcess"/> used to start or connect to the server process.
        /// </param>
        public DebugAdapterClient(ILoggerFactory loggerFactory, ServerProcess process)
            : this(loggerFactory)
        {
            if ( process == null )
                throw new ArgumentNullException(nameof(process));

            _process = process;
            _process.Exited.Subscribe(x => ServerProcess_Exit());
        }

        /// <summary>
        ///     Create a new <see cref="DebugAdapterClient"/>.
        /// </summary>
        /// <param name="loggerFactory">
        ///     The factory for loggers used by the client and its components.
        /// </param>
        DebugAdapterClient(ILoggerFactory loggerFactory)
        {
            if ( loggerFactory == null )
                throw new ArgumentNullException(nameof(loggerFactory));

            LoggerFactory = loggerFactory;
            Log = LoggerFactory.CreateLogger<DebugAdapterClient>();

            _dispatcher = new DapDispatcher(_serializer);
        }

        /// <summary>
        ///     Dispose of resources being used by the client.
        /// </summary>
        public void Dispose()
        {
            var connection = Interlocked.Exchange(ref _connection, null);
            connection?.Dispose();

            var serverProcess = Interlocked.Exchange(ref _process, null);
            serverProcess?.Dispose();
        }

        /// <summary>
        ///     The factory for loggers used by the client and its components.
        /// </summary>
        internal ILoggerFactory LoggerFactory { get; }

        /// <summary>
        ///     The client's logger.
        /// </summary>
        ILogger Log { get; }

        /// <summary>
        ///     Has the language client been initialised?
        /// </summary>
        public bool IsInitialized { get; private set; }

        /// <summary>
        ///     Is the connection to the debug adapter open?
        /// </summary>
        public bool IsConnected => _connection != null && _connection.IsOpen;

        /// <summary>
        ///     A <see cref="Task"/> that completes when the client is ready to handle requests.
        /// </summary>
        public Task IsReady => _readyCompletion.Task;

        /// <summary>
        ///     A <see cref="Task"/> that completes when the underlying connection has closed and the server has stopped.
        /// </summary>
        public Task HasShutdown
        {
            get {
                return Task.WhenAll(
                    _connection.HasHasDisconnected,
                    _process?.HasExited ?? Task.CompletedTask
                );
            }
        }

        /// <summary>
        ///     Get or create a sub-client of the specified type.
        /// </summary>
        /// <typeparam name="TClient">
        ///     The client type (must have a public constructor taking a single parameter of type <see cref="DebugAdapterClient"/>).
        /// </typeparam>
        /// <returns>
        ///     The sub-client.
        /// </returns>
        public TClient GetClient<TClient>()
            where TClient : DapClientBase
        {
            Type clientType = typeof(TClient);

            return (TClient) _clients.GetOrAdd(clientType, _ => {
                ConstructorInfo constructor = clientType.GetConstructor(new Type[] { DebugAdapterClientType });
                if ( constructor == null )
                    throw new InvalidOperationException($"Cannot create DAP sub-client of type '{clientType.FullName}' (the class is missing a public constructor that takes a single parameter of type '{DebugAdapterClientType.FullName}').");

                return (DapClientBase) constructor.Invoke(new object[] { this });
            });
        }

        /// <summary>
        ///     Initialise the debug adapter.
        /// </summary>
        /// <param name="workspaceRoot">
        ///     The workspace root.
        /// </param>
        /// <param name="initializationOptions">
        ///     An optional <see cref="object"/> representing additional options to send to the server.
        /// </param>
        /// <param name="cancellationToken">
        ///     An optional <see cref="CancellationToken"/> that can be used to cancel the operation.
        /// </param>
        /// <returns>
        ///     A <see cref="Task"/> representing initialisation.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        ///     <see cref="Initialize(string, object, CancellationToken)"/> has already been called.
        ///
        ///     <see cref="Initialize(string, object, CancellationToken)"/> can only be called once per <see cref="DebugAdapterClient"/>; if you have called <see cref="Shutdown"/>, you will need to use a new <see cref="DebugAdapterClient"/>.
        /// </exception>
        public async Task Initialize(InitializeRequestArguments arguments, CancellationToken cancellationToken = default)
        {
            if ( IsInitialized )
                throw new InvalidOperationException("Client has already been initialised.");

            if ( arguments == null )
                throw new ArgumentNullException(nameof(arguments));

            try
            {
                await Start();

                Log.LogDebug("Sending 'initialize' message to debug adapter...");

                // AF: Not certain that this pattern is correct for DAP (unlike LSP, there is an "initialized" event that we need to wait for).
                //
                // We *might* also need to wait for that event before calling _readyCompletion.TrySetResult.
                InitializeResponse result = await SendRequest<InitializeResponse>("initialize", arguments, cancellationToken).ConfigureAwait(false);
                if ( result == null )
                    throw new DapException("Server replied to 'initialize' request with a null response.");

                Log.LogDebug("Sent 'initialize' message to debug adapter.");

                Log.LogDebug("Sending 'initialized' notification to debug adapter...");

                SendEvent("initialized");

                Log.LogDebug("Sent 'initialized' notification to debug adapter.");

                IsInitialized = true;
                _readyCompletion.TrySetResult(null);
            }
            catch ( Exception initializationError )
            {
                // Capture the initialisation error so anyone awaiting IsReady will also see it.
                _readyCompletion.TrySetException(initializationError);

                throw;
            }
        }

        /// <summary>
        ///     Stop the debug adapter.
        /// </summary>
        /// <returns>
        ///     A <see cref="Task"/> representing the shutdown operation.
        /// </returns>
        public async Task Shutdown()
        {
            DapConnection connection = _connection;
            if ( connection != null )
            {
                if ( connection.IsOpen )
                {
                    connection.SendEvent("shutdown");
                    connection.SendEvent("exit");
                    connection.Disconnect(flushOutgoing: true);
                }

                await connection.HasHasDisconnected;
            }

            var serverProcess = _process;
            if ( serverProcess != null )
            {
                if ( serverProcess.IsRunning )
                    await serverProcess.Stop();
            }

            IsInitialized = false;
            _readyCompletion = new TaskCompletionSource<object>();
        }

        /// <summary>
        ///     Register a message handler.
        /// </summary>
        /// <param name="handler">
        ///     The message handler.
        /// </param>
        /// <returns>
        ///     An <see cref="IDisposable"/> representing the registration.
        /// </returns>
        public IDisposable RegisterHandler(IHandler handler) => _dispatcher.RegisterHandler(handler);

        /// <summary>
        ///     Send an event to the debug adapter.
        /// </summary>
        /// <param name="eventType">
        ///     The event type (name).
        /// </param>
        public void SendEvent(string eventType)
        {
            DapConnection connection = _connection;
            if ( connection == null || !connection.IsOpen )
                throw new InvalidOperationException("Not connected to the debug adapter.");

            connection.SendEvent(eventType);
        }

        /// <summary>
        ///     Send a notification message to the debug adapter.
        /// </summary>
        /// <param name="eventType">
        ///     The event type (name).
        /// </param>
        /// <param name="body">
        ///     The event body.
        /// </param>
        public void SendNotification(string eventType, object body)
        {
            DapConnection connection = _connection;
            if ( connection == null || !connection.IsOpen )
                throw new InvalidOperationException("Not connected to the debug adapter.");

            connection.SendEvent(eventType, body);
        }

        /// <summary>
        ///     Send a request to the debug adapter.
        /// </summary>
        /// <param name="method">
        ///     The request command name.
        /// </param>
        /// <param name="request">
        ///     The request message.
        /// </param>
        /// <param name="cancellationToken">
        ///     An optional cancellation token that can be used to cancel the request.
        /// </param>
        /// <returns>
        ///     A <see cref="Task"/> representing the request.
        /// </returns>
        public Task SendRequest(string method, object request, CancellationToken cancellationToken = default)
        {
            DapConnection connection = _connection;
            if ( connection == null || !connection.IsOpen )
                throw new InvalidOperationException("Not connected to the debug adapter.");

            return connection.SendRequest(method, request, cancellationToken);
        }

        /// <summary>
        ///     Send a request to the debug adapter.
        /// </summary>
        /// <typeparam name="TResponse">
        ///     The response message type.
        /// </typeparam>
        /// <param name="command">
        ///     The request command name.
        /// </param>
        /// <param name="arguments">
        ///     The request arguments.
        /// </param>
        /// <param name="cancellation">
        ///     An optional cancellation token that can be used to cancel the request.
        /// </param>
        /// <returns>
        ///     A <see cref="Task{TResult}"/> representing the response.
        /// </returns>
        public Task<TResponse> SendRequest<TResponse>(string command, object arguments, CancellationToken cancellation = default)
        {
            DapConnection connection = _connection;
            if ( connection == null || !connection.IsOpen )
                throw new InvalidOperationException("Not connected to the debug adapter.");

            return connection.SendRequest<TResponse>(command, arguments, cancellation);
        }

        /// <summary>
        ///     Start the debug adapter.
        /// </summary>
        /// <returns>
        ///     A <see cref="Task"/> representing the operation.
        /// </returns>
        async Task Start()
        {
            if ( _process == null )
                throw new ObjectDisposedException(GetType().Name);

            if ( !_process.IsRunning )
            {
                Log.LogDebug("Starting debug adapter...");

                await _process.Start();

                Log.LogDebug("Language server is running.");
            }

            Log.LogDebug("Opening connection to debug adapter...");

            if ( _connection == null )
                _connection = new DapConnection(LoggerFactory, input: _process.OutputStream, output: _process.InputStream);

            _connection.Connect(_dispatcher);

            Log.LogDebug("Connection to debug adapter is open.");
        }

        /// <summary>
        ///     Called when the server process has exited.
        /// </summary>
        /// <param name="sender">
        ///     The event sender.
        /// </param>
        /// <param name="args">
        ///     The event arguments.
        /// </param>
        async void ServerProcess_Exit()
        {
            Log.LogDebug("Server process has exited; language client is shutting down...");

            DapConnection connection = Interlocked.Exchange(ref _connection, null);
            if ( connection != null )
            {
                using ( connection )
                {
                    connection.Disconnect();
                    await connection.HasHasDisconnected;
                }
            }

            await Shutdown();

            Log.LogDebug("Language client shutdown complete.");
        }
    }
}
