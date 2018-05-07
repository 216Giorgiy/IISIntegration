using System;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.HttpSys.Internal;

namespace Microsoft.AspNetCore.Server.IISIntegration
{
    internal partial class WebSocketsAsyncIOEngine
    {
        internal sealed class WebSocketWriteOperation : AsyncWriteOperationBase
        {

            private static readonly NativeMethods.PFN_WEBSOCKET_ASYNC_COMPLETION WriteCallback = (httpContext, completionInfo, completionContext) =>
            {
                var context = (WebSocketWriteOperation)GCHandle.FromIntPtr(completionContext).Target;

                NativeMethods.HttpGetCompletionInfo(completionInfo, out var cbBytes, out var hr);

                var continuation = context.NotifyCompletion(hr, cbBytes);
                continuation.Invoke();

                return NativeMethods.REQUEST_NOTIFICATION_STATUS.RQ_NOTIFICATION_PENDING;
            };

            private readonly WebSocketsAsyncIOEngine _engine;
            private readonly GCHandle _thisHandle;

            public WebSocketWriteOperation(WebSocketsAsyncIOEngine engine)
            {
                _engine = engine;
                _thisHandle = GCHandle.Alloc(this);
            }

            internal override unsafe int WriteChunks(IntPtr requestHandler, int chunkCount, HttpApiTypes.HTTP_DATA_CHUNK* dataChunks, out bool completionExpected)
            {
                return NativeMethods.HttpWebsocketsWriteBytes(requestHandler, dataChunks, chunkCount, WriteCallback, (IntPtr)_thisHandle, out completionExpected);
            }

            public override void ResetOperation()
            {
                base.ResetOperation();

                _engine.ReturnOperation(this);
            }
        }
    }
}
