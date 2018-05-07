using System;
using System.Buffers;
using Microsoft.AspNetCore.HttpSys.Internal;

namespace Microsoft.AspNetCore.Server.IISIntegration
{
    internal abstract class AsyncWriteOperationBase : AsyncIOOperation
    {
        private const int HttpDataChunkStackLimit = 128; // 16 bytes per HTTP_DATA_CHUNK

        private IntPtr _requestHandler;
        private ReadOnlySequence<byte> _buffer;
        private MemoryHandle[] _handles;

        public void Initialize(IntPtr requestHandler, ReadOnlySequence<byte> buffer)
        {
            _requestHandler = requestHandler;
            _buffer = buffer;
        }

        public override unsafe bool InvokeOperation()
        {
            if (_buffer.Length > int.MaxValue)
            {
                throw new InvalidOperationException($"Writes larger then {int.MaxValue} are not supported.");
            }

            bool completionExpected;
            int hr;
            var chunkCount = GetChunkCount();


            var bufferLength = (int)_buffer.Length;

            if (chunkCount < HttpDataChunkStackLimit)
            {
                // To avoid stackoverflows, we will only stackalloc if the write size is less than the StackChunkLimit
                // The stack size is IIS is by default 128/256 KB, so we are generous with this threshold.
                var chunks = stackalloc HttpApiTypes.HTTP_DATA_CHUNK[chunkCount];
                hr = WriteSequence(chunkCount, _buffer, chunks, out completionExpected);
            }
            else
            {
                // Otherwise allocate the chunks on the heap.
                var chunks = new HttpApiTypes.HTTP_DATA_CHUNK[chunkCount];
                fixed (HttpApiTypes.HTTP_DATA_CHUNK* pDataChunks = chunks)
                {
                    hr = WriteSequence(chunkCount, _buffer, pDataChunks, out completionExpected);
                }
            }

            if (!completionExpected)
            {
                SetResult(hr, bufferLength);
                return true;
            }

            return false;
        }

        public override void FreeOperationResources(int hr, int bytes)
        {
            // Free the handles
            foreach (var handle in _handles)
            {
                handle.Dispose();
            }
        }

        public override void ResetOperation()
        {
            _requestHandler = default;
            _buffer = default;
            _handles.AsSpan().Clear();
        }

        private int GetChunkCount()
        {
            if (_buffer.IsSingleSegment)
            {
                return 1;
            }

            var count = 0;

            foreach (var _ in _buffer)
            {
                count++;
            }

            return count;
        }

        private unsafe int WriteSequence(int nChunks, ReadOnlySequence<byte> buffer, HttpApiTypes.HTTP_DATA_CHUNK* pDataChunks, out bool fCompletionExpected)
        {
            var currentChunk = 0;

            // REVIEW: We don't really need this list since the memory is already pinned with the default pool,
            // but shouldn't assume the pool implementation right now. Unfortunately, this causes a heap allocation...
            _handles = new MemoryHandle[nChunks];

            foreach (var readOnlyMemory in buffer)
            {
                ref var handle = ref _handles[currentChunk];
                ref var chunk = ref pDataChunks[currentChunk];
                handle = readOnlyMemory.Pin();

                chunk.DataChunkType = HttpApiTypes.HTTP_DATA_CHUNK_TYPE.HttpDataChunkFromMemory;
                chunk.fromMemory.BufferLength = (uint)readOnlyMemory.Length;
                chunk.fromMemory.pBuffer = (IntPtr)handle.Pointer;

                currentChunk++;
            }

            return WriteChunks(_requestHandler, nChunks, pDataChunks, out fCompletionExpected);
        }

        internal abstract unsafe int WriteChunks(IntPtr requestHandler, int chunkCount, HttpApiTypes.HTTP_DATA_CHUNK* dataChunks, out bool completionExpected);
    }
}
