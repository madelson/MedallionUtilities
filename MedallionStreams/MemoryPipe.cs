using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Medallion.IO
{
    public sealed class MemoryPipe
    {
        private static readonly Task<int> ZeroTask = Task.FromResult(0);
        
        private readonly object @lock = new object();

        private readonly int? maxBufferSize;
        private readonly byte[] baseBuffer;
        private readonly SemaphoreSlim bytesAvailableSignal, spaceAvailableSignal;

        private byte[] buffer;
        private ArraySegment<byte> handoffBuffer;
        private int readPointer, writePointer, count;
        private bool readSizeClosed, writeSideClosed;
        private int readTimeout = Timeout.Infinite,
            writeTimeout = Timeout.Infinite;
        private Task<int> readTask = ZeroTask;
        private Task writeTask = ZeroTask; 

        private Task<int> ReadAsync(ArraySegment<byte> outputBuffer, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            lock (this.@lock)
            {
                if (this.writeSideClosed) { return ZeroTask; }
                if (!this.readTask.IsCompleted) { throw new InvalidOperationException("The pipe does not support concurrent reads"); }

                // try to complete synchronously 
                var bytesRead = this.TryReadNoLock(outputBuffer);
                if (bytesRead > 0) { return Task.FromResult(bytesRead); }

                return this.readTask = this.WaitAndReadAsyncNoLock(outputBuffer, cancellationToken);
            }
        }

        private async Task<int> WaitAndReadAsyncNoLock(ArraySegment<byte> outputBuffer, CancellationToken cancellationToken)
        {
            if (!await this.bytesAvailableSignal.WaitAsync(this.readTimeout, cancellationToken).ConfigureAwait(false))
            {
                throw new TimeoutException("Timed out waiting for bytes to be available");
            };

            ArraySegment<byte> nextOutputBuffer;
            lock (this.@lock)
            {
                var bytesRead = this.TryReadNoLock(outputBuffer);
                if (bytesRead == 0) { throw new InvalidOperationException("sanity check"); }
                return bytesRead;
            }
        }

        private int TryReadNoLock(ArraySegment<byte> outputBuffer)
        {
            if (this.count == 0) { return 0; }

            var bytesToRead = Math.Min(this.count, outputBuffer.Count);

            if (this.maxBufferSize == 0)
            {
                if (this.handoffBuffer.Count == 0) { return 0; }

                Array.Copy(
                    sourceArray: this.handoffBuffer.Array,
                    sourceIndex: this.handoffBuffer.Offset,
                    destinationArray: outputBuffer.Array,
                    destinationIndex: outputBuffer.Offset,
                    length: bytesToRead
                );

                this.count -= bytesToRead;
                if (this.count == 0)
                {
                    this.handoffBuffer = default(ArraySegment<byte>);
                    this.spaceAvailableSignal.Release();
                }
                else
                {
                    this.handoffBuffer = new ArraySegment<byte>(this.handoffBuffer.Array, this.handoffBuffer.Offset + bytesToRead, this.count);
                }

                return bytesToRead;
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private Task<int> WriteAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (this.@lock)
            {
                if (this.readSizeClosed) { return ZeroTask; }
                if (!this.writeTask.IsCompleted) { throw new InvalidOperationException("The pipe does not support concurrent writes"); }

                // try to complete synchronously
            }

            throw new NotImplementedException("TODO");
        }

        private async Task<int> WriteAsyncNoLock(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            throw new NotImplementedException("TODO");
        }

        private void CloseReadSide() { }

        private void CloseWriteSide() { }

        #region ---- Input Stream ----
        private sealed class PipeInputStream : StreamBase
        {
            private readonly MemoryPipe pipe;

            public PipeInputStream(MemoryPipe pipe)
                : base(new StreamCapabilities { CanAsyncRead = true, CanTimeout = true })
            {
                this.pipe = pipe;
            }

            protected override Task<int> InternalReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                return this.pipe.ReadAsync(new ArraySegment<byte>(buffer, offset, count), cancellationToken);
            }

            protected override TimeSpan InternalReadTimeout
            {
                get { lock (this.pipe.@lock) { return TimeSpan.FromMilliseconds(this.pipe.readTimeout); } }
                set { lock (this.pipe.@lock) { this.pipe.readTimeout = (int)value.TotalMilliseconds; } }
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing) { this.pipe.CloseReadSide(); }

                base.Dispose(disposing);
            }
        }
        #endregion
    }
}
