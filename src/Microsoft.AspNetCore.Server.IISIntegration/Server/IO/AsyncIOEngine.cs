using System;
using System.Buffers;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Server.IISIntegration
{
    internal partial class AsyncIOEngine : IAsyncIOEngine
    {
        private readonly IntPtr _handler;

        private AsyncIOOperation _nextOperation;
        private AsyncIOOperation _runningOperation;

        private AsyncReadOperation _cachedAsyncReadOperation;
        private AsyncWriteOperation _cachedAsyncWriteOperation;
        private AsyncFlushOperation _cachedAsyncFlushOperation;

        public AsyncIOEngine(IntPtr handler)
        {
            _handler = handler;
        }

        public ValueTask<int> ReadAsync(Memory<byte> memory)
        {
            var read = GetReadOperation();
            read.Initialize(_handler, memory);
            Run(read);
            return new ValueTask<int>(read, 0);
        }

        public ValueTask<int> WriteAsync(ReadOnlySequence<byte> data)
        {
            var write = GetWriteOperation();
            write.Initialize(_handler, data);
            Run(write);
            CancelPendingRead();
            return new ValueTask<int>(write, 0);
        }

        private void Run(AsyncIOOperation ioOperation)
        {
            lock (this)
            {
                if (_runningOperation != null)
                {
                    if (_nextOperation == null)
                    {
                        _nextOperation = ioOperation;
                    }
                    else
                    {
                        throw new InvalidOperationException("Only one queued operation is allowed");
                    }
                }
                else
                {
                    // we are just starting operation so there would be no
                    // continuation registered
                    var completed = ioOperation.Invoke() != null;

                    // operation went async
                    if (!completed)
                    {
                        _runningOperation = ioOperation;
                    }
                }
            }
        }

        private void CancelPendingRead()
        {
            lock (this)
            {
                if (_runningOperation is AsyncReadOperation && _nextOperation != null)
                {
                    NativeMethods.HttpTryCancelIO(_handler);
                }
            }
        }

        public ValueTask FlushAsync()
        {
            var flush = GetFlushOperation();
            flush.Initialize(_handler);
            Run(flush);
            return new ValueTask(flush, 0);
        }

        public void NotifyCompletion(int hr, int bytes)
        {
            AsyncIOOperation.AsyncContinuation continuation;
            AsyncIOOperation.AsyncContinuation? nextContinuation = null;

            lock (this)
            {
                Debug.Assert(_runningOperation != null);

                continuation = _runningOperation.NotifyCompletion(hr, bytes);

                var next = _nextOperation;
                _nextOperation = null;
                _runningOperation = null;

                if (next != null)
                {
                    nextContinuation = next.Invoke();

                    // operation went async
                    if (nextContinuation == null)
                    {
                        _runningOperation = next;
                    }
                }
            }

            continuation.Invoke();
            nextContinuation?.Invoke();
        }

        public void Stop()
        {
            lock (this)
            {
                if (_runningOperation != null || _nextOperation != null)
                {
                    throw new InvalidOperationException();
                }
            }
        }

        private AsyncReadOperation GetReadOperation() =>
            Interlocked.Exchange(ref _cachedAsyncReadOperation, null) ??
            new AsyncReadOperation(this);

        private AsyncWriteOperation GetWriteOperation() =>
            Interlocked.Exchange(ref _cachedAsyncWriteOperation, null) ??
            new AsyncWriteOperation(this);

        private AsyncFlushOperation GetFlushOperation() =>
            Interlocked.Exchange(ref _cachedAsyncFlushOperation, null) ??
            new AsyncFlushOperation(this);

        private void ReturnOperation(AsyncReadOperation operation)
        {
            Volatile.Write(ref _cachedAsyncReadOperation, operation);
        }

        private void ReturnOperation(AsyncWriteOperation operation)
        {
            Volatile.Write(ref _cachedAsyncWriteOperation, operation);
        }

        private void ReturnOperation(AsyncFlushOperation operation)
        {
            Volatile.Write(ref _cachedAsyncFlushOperation, operation);
        }
    }
}
