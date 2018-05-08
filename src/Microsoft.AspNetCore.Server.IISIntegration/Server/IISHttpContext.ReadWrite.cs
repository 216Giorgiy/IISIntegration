// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Server.IISIntegration
{
    internal partial class IISHttpContext
    {
        private bool _wasUpgraded;

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
                await InitializeResponseAwaited();
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
        internal Task WriteAsync(ReadOnlyMemory<byte> memory, CancellationToken cancellationToken = default(CancellationToken))
        {
            async Task WriteFirstAsync()
            {
                await InitializeResponseAwaited();
                await Output.WriteAsync(memory, cancellationToken);
            }

            return !_hasResponseStarted ? WriteFirstAsync() : Output.WriteAsync(memory, cancellationToken);
        }

        /// <summary>
        /// Flushes the data in the output pipe
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        internal Task FlushAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            async Task FlushFirstAsync()
            {
                await InitializeResponseAwaited();
                await Output.FlushAsync(cancellationToken);
            }

            return !_hasResponseStarted ? FlushFirstAsync() : Output.FlushAsync(cancellationToken);
        }

        private void StartProcessingRequestAndResponseBody()
        {
            if (_processBodiesTask == null)
            {
                lock (_createReadWriteBodySync)
                {
                    if (_processBodiesTask == null)
                    {
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

        private Task StartBidirectionalStream()
        {
            // IIS allows for websocket support and duplex channels only on Win8 and above
            // This allows us to have two tasks for reading the request and writing the response
            var readWebsocketTask = ReadBody();
            var writeWebsocketTask = WriteBody();
            return Task.WhenAll(readWebsocketTask, writeWebsocketTask);
        }

        private async Task ReadBody()
        {
            try
            {
                while (true)
                {
                    var memory = Input.Writer.GetMemory();

                    var read = await AsyncIO.ReadAsync(memory);

                    if (read == -1)
                    {
                        continue;
                    }

                    if (read == 0)
                    {
                        break;
                    }

                    Input.Writer.Advance(read);

                    var result = await Input.Writer.FlushAsync();

                    if (result.IsCompleted || result.IsCanceled)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Input.Writer.Complete(ex);
            }
            finally
            {
                Input.Writer.Complete();
            }
        }

        private async Task WriteBody()
        {
            try
            {
                while (true)
                {
                    var result = await Output.Reader.ReadAsync();

                    var buffer = result.Buffer;

                    try
                    {
                        if (!buffer.IsEmpty)
                        {
                            await AsyncIO.WriteAsync(buffer);
                        }

                        if (result.IsCanceled)
                        {
                            await AsyncIO.FlushAsync();
                        }

                        if (result.IsCompleted)
                        {
                            break;
                        }
                    }
                    finally
                    {
                        Output.Reader.AdvanceTo(buffer.End);
                    }
                }
            }
            catch (Exception ex)
            {
                Output.Reader.Complete(ex);
            }
            finally
            {
                Output.Reader.Complete();
            }
        }
    }
}
