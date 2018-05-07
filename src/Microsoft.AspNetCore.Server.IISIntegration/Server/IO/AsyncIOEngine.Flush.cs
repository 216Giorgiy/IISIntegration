using System;

namespace Microsoft.AspNetCore.Server.IISIntegration
{
    internal partial class AsyncIOEngine
    {
        internal class AsyncFlushOperation : AsyncIOOperation
        {
            private readonly AsyncIOEngine _engine;

            private IntPtr _requestHandler;

            public AsyncFlushOperation(AsyncIOEngine engine)
            {
                _engine = engine;
            }

            public void Initialize(IntPtr requestHandler)
            {
                _requestHandler = requestHandler;
            }

            public override bool InvokeOperation()
            {
                var hr = NativeMethods.HttpFlushResponseBytes(_requestHandler, out var fCompletionExpected);
                if (!fCompletionExpected)
                {
                    SetResult(hr, 0);
                    return true;
                }

                return false;
            }

            public override void FreeOperationResources(int hr, int bytes)
            {
            }

            protected override void ResetOperation()
            {
                base.ResetOperation();

                _requestHandler = default;
                _engine.ReturnOperation(this);
            }
        }
    }
}
