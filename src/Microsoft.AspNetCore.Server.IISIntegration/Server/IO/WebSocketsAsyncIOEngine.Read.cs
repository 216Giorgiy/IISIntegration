using System;
using System.Buffers;
using System.Runtime.InteropServices;

namespace Microsoft.AspNetCore.Server.IISIntegration
{
    internal partial class WebSocketsAsyncIOEngine
    {
        internal class WebSocketReadOperation : AsyncIOOperation
        {
            public static readonly NativeMethods.PFN_WEBSOCKET_ASYNC_COMPLETION ReadCallback = (httpContext, completionInfo, completionContext) =>
            {
                var context = (WebSocketReadOperation)GCHandle.FromIntPtr(completionContext).Target;

                NativeMethods.HttpGetCompletionInfo(completionInfo, out var cbBytes, out var hr);

                var continuation = context.NotifyCompletion(hr, cbBytes);

                continuation.Invoke();

                return NativeMethods.REQUEST_NOTIFICATION_STATUS.RQ_NOTIFICATION_PENDING;
            };

            private readonly WebSocketsAsyncIOEngine _engine;
            private readonly GCHandle _thisHandle;
            private MemoryHandle _inputHandle;
            private IntPtr _requestHandler;
            private Memory<byte> _memory;

            public WebSocketReadOperation(WebSocketsAsyncIOEngine engine)
            {
                _engine = engine;
                _thisHandle = GCHandle.Alloc(this);
            }

            public override unsafe bool InvokeOperation()
            {
                _inputHandle = _memory.Pin();

                var hr = NativeMethods.HttpWebsocketsReadBytes(
                    _requestHandler,
                    (byte*)_inputHandle.Pointer,
                    _memory.Length,
                    ReadCallback,
                    (IntPtr)_thisHandle,
                    out var dwReceivedBytes,
                    out var fCompletionExpected);

                if (!fCompletionExpected)
                {
                    SetResult(hr, dwReceivedBytes);
                    return true;
                }

                return false;
            }

            public void Initialize(IntPtr requestHandler, Memory<byte> memory)
            {
                _requestHandler = requestHandler;
                _memory = memory;
            }

            public override void FreeOperationResources(int hr, int bytes)
            {
                _inputHandle.Dispose();
            }

            public override void ResetOperation()
            {
                _memory = default;
                _inputHandle.Dispose();
                _inputHandle = default;
                _requestHandler = default;

                _engine.ReturnOperation(this);
            }
        }
    }
}
