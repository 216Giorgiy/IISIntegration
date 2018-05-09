// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks.Sources;

namespace Microsoft.AspNetCore.Server.IISIntegration
{
    internal abstract class AsyncIOOperation: IValueTaskSource<int>, IValueTaskSource
    {
        private static readonly Action<object> CallbackCompleted = _ => { Debug.Assert(false, "Should not be invoked"); };

        private Action<object> _continuation;
        private object _state;
        private int _result;

        private Exception _exception;

        public ValueTaskSourceStatus GetStatus(short token)
        {
            if (ReferenceEquals(Volatile.Read(ref _continuation), CallbackCompleted))
            {
                return ValueTaskSourceStatus.Pending;
            }

            return _exception != null ? ValueTaskSourceStatus.Succeeded : ValueTaskSourceStatus.Faulted;
        }

        public void OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            if (_state != null)
            {
                ThrowMultipleContinuations();
            }

            _state = state;

            var previousContinuation = Interlocked.CompareExchange(ref _continuation, continuation, null);

            if (previousContinuation != null)
            {
                if (!ReferenceEquals(previousContinuation, CallbackCompleted))
                {
                    ThrowMultipleContinuations();
                }

                new AsyncContinuation(continuation, state).Invoke();
            }
        }

        private static void ThrowMultipleContinuations()
        {
            throw new InvalidOperationException("Multiple awaiters are not allowed");
        }

        void IValueTaskSource.GetResult(short token)
        {
            var exception = _exception;

            ResetOperation();

            if (exception != null)
            {
                throw exception;
            }
        }

        public int GetResult(short token)
        {
            var exception = _exception;
            var result = _result;

            ResetOperation();

            if (exception != null)
            {
                throw exception;
            }

            return result;
        }

        public AsyncContinuation? Invoke()
        {
            if (InvokeOperation())
            {
                return new AsyncContinuation(_continuation, _state);
            }
            return null;
        }

        public abstract bool InvokeOperation();

        public AsyncContinuation Complete(int hr, int bytes)
        {
            SetResult(hr, bytes);

            var continuation = Interlocked.CompareExchange(ref _continuation, CallbackCompleted, null);
            if (continuation != null)
            {
                var state = _state;
                return new AsyncContinuation(continuation, state);
            }

            return default;
        }

        protected void SetResult(int hr, int bytes)
        {
            if (hr != NativeMethods.HR_CANCEL_IO)
            {
                _result = bytes;
                if (hr != NativeMethods.HR_OK)
                {
                    _exception = new IOException("IO exception occurred", hr);
                }
            }
            else
            {
                _result = -1;
                _exception = null;
            }

            FreeOperationResources(hr, bytes);
        }

        public abstract void FreeOperationResources(int hr, int bytes);

        protected virtual void ResetOperation()
        {
            _exception = null;
            _result = int.MinValue;
            _state = null;
            _continuation = null;
        }

        public readonly struct AsyncContinuation
        {
            public Action<object> Continuation { get; }
            public object State { get; }

            public AsyncContinuation(Action<object> continuation, object state)
            {
                Continuation = continuation;
                State = state;
            }

            public void Invoke()
            {
                if (Continuation != null)
                {
                    // TODO: use generic overload when code moved to be netcoreapp only
                    var continuation = Continuation;
                    var state = State;
                    ThreadPool.QueueUserWorkItem(_ => continuation(state));
                }
            }
        }
    }
}
