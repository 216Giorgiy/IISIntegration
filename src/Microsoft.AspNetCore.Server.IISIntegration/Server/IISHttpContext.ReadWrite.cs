// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Server.IISIntegration
{
    internal partial class IISHttpContext
    {
        /// <summary>
        /// Reads data from the Input pipe to the user.
        /// </summary>
        /// <param name="memory"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        internal async Task<int> ReadAsync(Memory<byte> memory, CancellationToken cancellationToken)
        {
            if (!_hasResponseStarted)
            {
                StartProcessingRequestAndResponseBody();
            }

            while (true)
            {
                var result = await Input.Reader.ReadAsync();
                var readableBuffer = result.Buffer;
                try
                {
                    if (!readableBuffer.IsEmpty)
                    {
                        var actual = Math.Min(readableBuffer.Length, memory.Length);
                        readableBuffer = readableBuffer.Slice(0, actual);
                        readableBuffer.CopyTo(memory.Span);
                        return (int)actual;
                    }
                    else if (result.IsCompleted)
                    {
                        return 0;
                    }
                }
                finally
                {
                    Input.Reader.AdvanceTo(readableBuffer.End, readableBuffer.End);
                }
            }
        }

        /// <summary>
        /// Writes data to the output pipe.
        /// </summary>
        /// <param name="memory"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        internal async Task WriteAsync(ReadOnlyMemory<byte> memory, CancellationToken cancellationToken = default(CancellationToken))
        {
            // Want to keep exceptions consistent,
            if (!_hasResponseStarted)
            {
                await InitializeResponseAwaited();
            }

            await Output.WriteAsync(memory, cancellationToken);
        }

        /// <summary>
        /// Flushes the data in the output pipe
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        internal async Task FlushAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            // Want to keep exceptions consistent,
            if (!_hasResponseStarted)
            {
                await InitializeResponseAwaited();
            }

            await AsyncIO.FlushAsync();
        }

        private void StartProcessingRequestAndResponseBody()
        {
            if (_processBodiesTask == null)
            {
                lock (_createReadWriteBodySync)
                {
                    if (_processBodiesTask == null)
                    {
                        // If at this point request was not upgraded just start a normal IO engine
                        if (AsyncIO == null)
                        {
                            AsyncIO = new AsyncIOEngine(_pInProcessHandler);
                        }

                        _processBodiesTask = ConsumeAsync();
                    }
                }
            }
        }

        // ConsumeAsync is called when either the first read or first write is done.
        // There are two modes for reading and writing to the request/response bodies without upgrade.
        // 1. Await all reads and try to read from the Output pipe
        // 2. Done reading and await all writes.
        // If the request is upgraded, we will start bidirectional streams for the input and output.
        private async Task ConsumeAsync()
        {
            await StartBidirectionalStream();
        }
    }
}
