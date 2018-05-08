// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.AspNetCore.Server.IISIntegration
{
    internal partial class WebSocketsAsyncIOEngine
    {
        internal class AsyncInitializeOperation : AsyncIOOperation
        {
            private readonly WebSocketsAsyncIOEngine _engine;

            private IntPtr _requestHandler;

            public AsyncInitializeOperation(WebSocketsAsyncIOEngine engine)
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
