// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Buffers;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Server.IISIntegration
{
    internal interface IAsyncIOEngine
    {
        ValueTask<int> ReadAsync(Memory<byte> memory);
        ValueTask<int> WriteAsync(ReadOnlySequence<byte> data);
        ValueTask FlushAsync();
        void NotifyCompletion(int hr, int bytes);
        void Stop();
    }
}
