using JsonRpc;
using Lsp.Models;
using Lsp.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json.Linq;

namespace Lsp
{
    public class LanguageClient
        : IDisposable
    {
        readonly Connection _connection;
        readonly LspRequestRouter _requestRouter;
        readonly HandlerCollection _handlers = new HandlerCollection();
        readonly IResponseRouter _responseRouter;
        readonly LspReciever _reciever;
        readonly TaskCompletionSource<InitializeResult> _initialized = new TaskCompletionSource<InitializeResult>();

        public LanguageClient(Stream input, Stream output)
            : this(input, new OutputHandler(output), new LspReciever(), new RequestProcessIdentifier())
        {
        }

        internal LanguageClient(Stream input, IOutputHandler output, LspReciever reciever, IRequestProcessIdentifier requestProcessIdentifier)
        {
            _reciever = reciever;
            _requestRouter = new LspRequestRouter(_handlers);
            _responseRouter = new ResponseRouter(output);
            _connection = new Connection(input, output, reciever, requestProcessIdentifier, _requestRouter, _responseRouter);
        }

        public async Task<InitializeResult> Initialize(InitializeParams parameters)
        {
            _connection.Open();

            InitializeResult result = await SendRequest<InitializeParams, InitializeResult>("initialize", parameters);
            _initialized.SetResult(result);

            return result;
        }

        public IDisposable AddHandler(IJsonRpcHandler handler)
        {
            return _handlers.Add(handler);
        }

        public void SendNotification<T>(string method, T @params)
        {
            _responseRouter.SendNotification(method, @params);
        }

        public Task<TResponse> SendRequest<T, TResponse>(string method, T @params)
        {
            return _responseRouter.SendRequest<T, TResponse>(method, @params);
        }

        public Task SendRequest<T>(string method, T @params)
        {
            return _responseRouter.SendRequest(method, @params);
        }

        public TaskCompletionSource<JToken> GetRequest(long id)
        {
            return _responseRouter.GetRequest(id);
        }

        public void Dispose()
        {
            _connection?.Dispose();
        }
    }
}
