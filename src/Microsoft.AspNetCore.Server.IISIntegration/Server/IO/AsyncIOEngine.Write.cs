using System;
using Microsoft.AspNetCore.HttpSys.Internal;

namespace Microsoft.AspNetCore.Server.IISIntegration
{
    internal partial class AsyncIOEngine
    {
        private class AsyncWriteOperation : AsyncWriteOperationBase
        {
            private readonly AsyncIOEngine _engine;

            public AsyncWriteOperation(AsyncIOEngine engine)
            {
                _engine = engine;
            }

            internal override unsafe int WriteChunks(IntPtr requestHandler, int chunkCount, HttpApiTypes.HTTP_DATA_CHUNK* dataChunks,
                out bool completionExpected)
            {
                return NativeMethods.HttpWriteResponseBytes(requestHandler, dataChunks, chunkCount, out completionExpected);
            }

            protected override void ResetOperation()
            {
                base.ResetOperation();

                _engine.ReturnOperation(this);
            }
        }
    }
}
