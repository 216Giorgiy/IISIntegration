// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Server.IISIntegration
{
    internal partial class WebSocketsAsyncIOEngine: IAsyncIOEngine
    {
        private readonly IntPtr _handler;

        private bool _isInitialized = false;

        private AsyncInitializeOperation _initializationFlush;

        private WebSocketWriteOperation _cachedWebSocketWriteOperation;

        private WebSocketReadOperation _cachedWebSocketReadOperation;

        private AsyncInitializeOperation _cachedAsyncInitializeOperation;

        public WebSocketsAsyncIOEngine(IntPtr handler)
        {
            _handler = handler;
        }

        public ValueTask Initialize()
        {
            if (_isInitialized)
            {
                throw new InvalidOperationException("Already initialized");
            }

            _initializationFlush = new AsyncInitializeOperation();
            _initializationFlush.Initialize(_handler);
            var continuation = _initializationFlush.Invoke();

            if (continuation != null)
            {
                _isInitialized = true;
            }

            return new ValueTask(_initializationFlush, 0);
        }

        public ValueTask<int> ReadAsync(Memory<byte> memory)
        {
            CheckInitialized();

            var read = new WebSocketReadOperation();
            read.Initialize(_handler, memory);
            read.Invoke();
            return new ValueTask<int>(read, 0);
        }

        public ValueTask<int> WriteAsync(ReadOnlySequence<byte> data)
        {
            var write = GetWriteOperation();
            write.Initialize(_handler, data);
            write.Invoke();
            return new ValueTask<int>(write, 0);
        }

        public ValueTask FlushAsync()
        {
            // WebSockets auto flush
            return new ValueTask(Task.CompletedTask);
        }

        public void NotifyCompletion(int hr, int bytes)
        {
            _isInitialized = true;
            if (_initializationFlush == null)
            {
                throw new InvalidOperationException("Unexpected completion for WebSocket operation");
            }

            var continuation = _initializationFlush.NotifyCompletion(hr, bytes);
            continuation.Invoke();
        }

        private void CheckInitialized()
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("IO not initialized yet");
            }
        }

        public void Stop()
        {
            // TODO
        }




        private WebSocketReadOperation GetReadOperation() =>
            Interlocked.Exchange(ref _cachedWebSocketReadOperation, null) ??
            new WebSocketReadOperation(this);

        private WebSocketWriteOperation GetWriteOperation() =>
            Interlocked.Exchange(ref _cachedWebSocketWriteOperation, null) ??
            new WebSocketWriteOperation(this);

        private AsyncInitializeOperation GetInitializeOperation() =>
            Interlocked.Exchange(ref _cachedAsyncInitializeOperation, null) ??
            new AsyncInitializeOperation(this);

        private void ReturnOperation(AsyncInitializeOperation operation)
        {
            Volatile.Write(ref _cachedAsyncInitializeOperation, operation);
        }

        private void ReturnOperation(WebSocketWriteOperation operation)
        {
            Volatile.Write(ref _cachedWebSocketWriteOperation, operation);
        }

        private void ReturnOperation(WebSocketReadOperation operation)
        {
            Volatile.Write(ref _cachedWebSocketReadOperation, operation);
        }
    }
}
