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
        /// <summary>
        /// Reads data from the Input pipe to the user.
        /// </summary>
        /// <param name="memory"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        internal async Task<int> ReadAsync(Memory<byte> memory, CancellationToken cancellationToken)
        {
            if (!HasResponseStarted)
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

            return !HasResponseStarted ? WriteFirstAsync() : Output.WriteAsync(memory, cancellationToken);
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

            return !HasResponseStarted ? FlushFirstAsync() : Output.FlushAsync(cancellationToken);
        }

        private void StartProcessingRequestAndResponseBody()
        {
            if (_processBodiesTask == null)
            {
                lock (_createReadWriteBodySync)
                {
                    if (_processBodiesTask == null)
                    {
                        _processBodiesTask = Task.WhenAll(ReadBody(), WriteBody());
                    }
                }
            }
        }

        private async Task ReadBody()
        {
            try
            {
                while (true)
                {
                    var memory = Input.Writer.GetMemory();

                    var read = await AsyncIO.ReadAsync(memory);

                    // Read was canceled because of incoming write, requeue again
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
