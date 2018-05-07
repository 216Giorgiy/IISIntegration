using System;
using System.Buffers;

namespace Microsoft.AspNetCore.Server.IISIntegration
{
    internal partial class AsyncIOEngine
    {
        internal class AsyncReadOperation : AsyncIOOperation
        {
            private readonly AsyncIOEngine _engine;

            private MemoryHandle _inputHandle;

            private IntPtr _requestHandler;

            private Memory<byte> _memory;

            public AsyncReadOperation(AsyncIOEngine engine)
            {
                _engine = engine;
            }

            public void Initialize(IntPtr requestHandler, Memory<byte> memory)
            {
                _requestHandler = requestHandler;
                _memory = memory;
            }

            public override unsafe bool InvokeOperation()
            {
                _inputHandle = _memory.Pin();
                var hr = NativeMethods.HttpReadRequestBytes(
                    _requestHandler,
                    (byte*)_inputHandle.Pointer,
                    _memory.Length,
                    out var dwReceivedBytes,
                    out bool fCompletionExpected);

                if (!fCompletionExpected)
                {
                    SetResult(hr, dwReceivedBytes);
                    return true;
                }

                return false;
            }

            public override void ResetOperation()
            {
                _memory = default;
                _inputHandle.Dispose();
                _inputHandle = default;
                _requestHandler = default;

                _engine.ReturnOperation(this);
            }

            public override void FreeOperationResources(int hr, int bytes)
            {
                _inputHandle.Dispose();
            }
        }
    }
}
