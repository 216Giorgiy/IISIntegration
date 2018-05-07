using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks.Sources;

namespace Microsoft.AspNetCore.Server.IISIntegration
{
    internal abstract class AsyncIOOperation: IValueTaskSource<int>, IValueTaskSource
    {
        private Action<object> _continuation;
        private object _state;
        private int _result;

        private bool _completed;

        private Exception _exception;

        public ValueTaskSourceStatus GetStatus(short token)
        {
            if (!_completed)
            {
                return ValueTaskSourceStatus.Pending;
            }

            return _exception != null ? ValueTaskSourceStatus.Succeeded : ValueTaskSourceStatus.Faulted;
        }

        public void OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            if (_completed)
            {
                continuation(state);
                return;
            }

            if (_continuation != null)
            {
                throw new InvalidOperationException();
            }

            _continuation = continuation;
            _state = state;
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


        public AsyncContinuation NotifyCompletion(int hr, int bytes)
        {
            SetResult(hr, bytes);
            return new AsyncContinuation(_continuation, _state);
        }

        protected void SetResult(int hr, int bytes)
        {
            _completed = true;
            _result = bytes;
            _exception = Marshal.GetExceptionForHR(hr);

            FreeOperationResources(hr, bytes);
        }


        public void Reset()
        {
            _exception = null;
            _result = int.MinValue;
            _state = null;
            _continuation = null;
        }

        public abstract void FreeOperationResources(int hr, int bytes);

        public abstract void ResetOperation();

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
