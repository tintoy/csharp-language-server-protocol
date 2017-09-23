using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace JsonRpc
{
    public class ProcessScheduler : IScheduler
    {
        private readonly BlockingCollection<(RequestProcessType type, Func<Task> request)> _queue;
        private readonly CancellationTokenSource _cancel;
        private readonly Thread _thread;

        public ProcessScheduler()
        {
            _queue = new BlockingCollection<(RequestProcessType type, Func<Task> request)>();
            _cancel = new CancellationTokenSource();
            _thread = new Thread(ProcessRequestQueue) { IsBackground = true, Name = "ProcessRequestQueue" };
        }

        public void Start()
        {
            _thread.Start();
        }

        public void Add(RequestProcessType type, Func<Task> request)
        {
            _queue.Add((type, request));
        }

        private Task Start(Func<Task> request)
        {
            var t = request();
            if (t.Status == TaskStatus.Created) // || t.Status = TaskStatus.WaitingForActivation ?
                t.Start();
            return t;
        }

        private List<Task> RemoveCompleteTasks(List<Task> list)
        {
            if (list.Count == 0) return list;

            var result = new List<Task>();
            foreach (var t in list)
            {
                if (t.IsFaulted)
                {
                    // TODO: Handle Fault
                }
                else if (!t.IsCompleted)
                {
                    Log.Verbose("Process Task {TaskId} is complete.", t.Id);

                    result.Add(t);
                }
            }
            return result;
        }

        public long _TestOnly_NonCompleteTaskCount = 0;
        private void ProcessRequestQueue()
        {
            // see https://github.com/OmniSharp/csharp-language-server-protocol/issues/4
            // no need to be async, because this thing already allocated a thread on it's own.
            var token = _cancel.Token;
            var waitables = new List<Task>();
            try
            {
                while (true)
                {
                    if (_queue.TryTake(out var item, Timeout.Infinite, token))
                    {
                        var (type, request) = item;
                        try
                        {
                            Log.Verbose("Processing scheduled {ProcessType} item...", type);
                            if (type == RequestProcessType.Serial)
                            {
                                Log.Verbose("Waiting for {ExistingProcessCount} existing processes to complete before starting Serial process...", waitables.Count);
                                Task[] currentWaitables = waitables.ToArray();
                                Task.WaitAll(currentWaitables, token);
                                Log.Verbose("{ExistingProcessCount} existing processes have completed; starting Serial process...", currentWaitables.Length);

                                Start(request).Wait(token);
                                Log.Verbose("Serial process completed.");
                            }
                            else if (type == RequestProcessType.Parallel)
                            {
                                Log.Verbose("Starting Parallel process...");

                                waitables.Add(Start(request));

                                Log.Verbose("Started Parallel process (there are now {ExistingProcessCount} processes running).", waitables.Count);
                            }
                            else
                                throw new NotImplementedException("Only Serial and Parallel execution types can be handled currently");

                            waitables = RemoveCompleteTasks(waitables);
                            Interlocked.Exchange(ref _TestOnly_NonCompleteTaskCount, waitables.Count);
                        }
                        catch (AggregateException processRequestError)
                        {
                            foreach (Exception exception in processRequestError.Flatten().InnerExceptions)
                                Log.Error(exception, "Error processing {RequestType} request: {ErrorMessage}", type, exception.Message);

                            throw;
                        }
                        catch (Exception processRequestError)
                        {
                            Log.Error(processRequestError, "Error processing {RequestType} request: {ErrorMessage}", type, processRequestError.Message);

                            throw;
                        }
                    }
                }
            }
            catch (OperationCanceledException ex)
            {
                if (ex.CancellationToken != token)
                    throw;
                // OperationCanceledException - The CancellationToken has been canceled.
                Task.WaitAll(waitables.ToArray(), TimeSpan.FromMilliseconds(1000));
                var keeponrunning = RemoveCompleteTasks(waitables);
                Interlocked.Exchange(ref _TestOnly_NonCompleteTaskCount, keeponrunning.Count);
                keeponrunning.ForEach((t) => {
                    // TODO: There is no way to abort a Task. As we don't construct the tasks, we can do nothing here
                    // Option is: change the task factory "Func<Task> request" to a "Func<CancellationToken, Task> request"
                });
            }
        }

        private bool _disposed = false;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _cancel.Cancel();
            _thread.Join();
            _cancel.Dispose();
        }
    }
}
