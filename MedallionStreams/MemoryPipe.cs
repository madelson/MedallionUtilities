using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Medallion.IO
{
    /// <summary>
    /// The <see cref="MemoryPipe"/> is an in-memory <see cref="Stream"/> which supports concurrent reading and writing.
    /// Unlike <see cref="MemoryStream"/>, the reader and writer maintain separate positions, so the reader will read
    /// the bytes in the same sequence as they were written
    /// </summary>
    public sealed class MemoryPipe : StreamBase
    {
        /// <summary>
        /// The size below which we will not consider shrinking the buffer even when it is mostly empty.
        /// This is non-zero to prevent thrashing
        /// </summary>
        private const int MaxStableBufferSize = 1024;

        private static readonly byte[] Empty = Enumerable.Empty<byte>() as byte[] ?? new byte[0];
        private static Task<int> CompletedZeroTask = Task.FromResult(0);

        private readonly object @lock = new object();
        private readonly int? maxCapacity;
        private readonly SemaphoreSlim bytesAvailable = new SemaphoreSlim(initialCount: 0, maxCount: 1), spaceAvailable;

        private byte[] buffer = Empty;
        private int start, count;
        private TimeSpan readTimeout = Timeout.InfiniteTimeSpan, writeTimeout = Timeout.InfiniteTimeSpan;
        private bool readerClosed, writerClosed;
        private Stream readSide, writeSide;

        public MemoryPipe(int? maxCapacity = null)
            // thread-safe since we support one reader and one writer concurrently
            : base(StreamCapabilities.Read | StreamCapabilities.Write | StreamCapabilities.Async | StreamCapabilities.Threadsafe | StreamCapabilities.Timeout)
        {
            if (maxCapacity.HasValue)
            {
                Throw.IfOutOfRange(maxCapacity.Value, nameof(maxCapacity), min: 1);
                this.maxCapacity = maxCapacity;
                this.spaceAvailable = new SemaphoreSlim(initialCount: 0, maxCount: 1);
            }
        }

        #region ---- StreamBase Overrides ----
        protected override Task<int> InternalReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return base.InternalReadAsync(buffer, offset, count, cancellationToken);
        }

        protected override Task InternalWriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return base.InternalWriteAsync(buffer, offset, count, cancellationToken);
        }

        protected override void InternalWrite(byte[] buffer, int offset, int count)
        {
            base.InternalWrite(buffer, offset, count);
        }

        protected override void InternalFlush()
        {
            // no-op
        }

        protected override Task InternalFlushAsync(CancellationToken cancellationToken)
        {
            // no-op
            return CompletedZeroTask;
        }

        protected override TimeSpan InternalReadTimeout
        {
            get { lock (this.@lock) { return this.readTimeout; } }
            set { lock (this.@lock) { this.readTimeout = value; } }
        }

        protected override TimeSpan InternalWriteTimeout
        {
            get { lock(this.@lock) { return this.writeTimeout; } }
            set { lock (this.@lock) { this.writeTimeout = value; } }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }
        #endregion

        #region ---- Internal Helpers ----
        /// <summary>
        /// MA: I used to have the <see cref="SemaphoreSlim"/>s updated in various ways and in various places
        /// throughout the code. Now I have just one function that sets both signals to the correct
        /// values. This is called from <see cref="ReadNoLock"/>, <see cref="WriteNoLock"/>, 
        /// <see cref="InternalCloseReadSideNoLock"/>, and <see cref="InternalCloseWriteSideNoLock"/>.
        /// 
        /// While it may seem like this does extra work, nearly all cases are necessary. For example, we used
        /// to say "signal bytes available if count > 0" at the end of <see cref="WriteNoLock"/>. The problem is
        /// that we could have the following sequence of operations:
        /// 1. <see cref="ReadNoLockAsync"/> blocks on <see cref="bytesAvailableSignal"/>
        /// 2. <see cref="WriteNoLock"/> writes and signals
        /// 3. <see cref="ReadNoLockAsync"/> wakes up
        /// 4. Another <see cref="WriteNoLock"/> call writes and re-signals
        /// 5. <see cref="ReadNoLockAsync"/> reads ALL content and returns, leaving <see cref="bytesAvailableSignal"/> signaled (invalid)
        /// 
        /// This new implementation avoids this because the <see cref="ReadNoLock"/> call inside <see cref="ReadNoLockAsync"/> will
        /// properly unsignal after it consumes ALL the contents
        /// </summary>
        private void UpdateSemaphoresNoLock()
        {
            // update bytes available
            switch (this.bytesAvailable.CurrentCount)
            {
                case 0:
                    if (this.count > 0 || this.writerClosed)
                    {
                        this.bytesAvailable.Release();
                    }
                    break;
                case 1:
                    if (this.count == 0 && !this.writerClosed)
                    {
                        this.bytesAvailable.Wait();
                    }
                    break;
                default:
                    throw new InvalidOperationException("Should never get here");
            }

            // update space available
            if (this.spaceAvailable != null)
            {
                switch (this.spaceAvailable.CurrentCount)
                {
                    case 0:
                        if (this.readerClosed || this.GetSpaceAvailableNoLock() > 0)
                        {
                            this.spaceAvailable.Release();
                        }
                        break;
                    case 1:
                        if (!this.readerClosed && this.GetSpaceAvailableNoLock() == 0)
                        {
                            this.spaceAvailable.Wait();
                        }
                        break;
                    default:
                        throw new InvalidOperationException("Should never get here");
                }
            }
        }

        private void CloseReadSide()
        {

        }

        private void CloseWriteSide()
        {
        }
        #endregion

        #region ---- Read Side ----
        public Stream ReadSide { get { lock (this.@lock) { return this.readSide ?? (this.readSide = new ReadSideStream(this)); } } }

        private sealed class ReadSideStream : StreamBase
        {
            private readonly MemoryPipe pipe;

            public ReadSideStream(MemoryPipe pipe)
                : base(StreamCapabilities.Read | StreamCapabilities.Async | StreamCapabilities.Timeout)
            {
                this.pipe = pipe;
            }

            protected override int InternalRead(byte[] buffer, int offset, int count)
            {
                return this.pipe.Read(buffer, offset, count);
            }

            protected override Task<int> InternalReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                return this.pipe.ReadAsync(buffer, offset, count, cancellationToken);
            }

            protected override TimeSpan InternalReadTimeout
            {
                get { return TimeSpan.FromMilliseconds(this.pipe.ReadTimeout); }
                set { this.pipe.ReadTimeout = (int)value.TotalMilliseconds; }
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    this.pipe.CloseReadSide();
                }
                base.Dispose(disposing);
            }
        }
        #endregion

        #region ---- Write Side ----
        public Stream WriteSide { get { lock(this.@lock) { return this.writeSide ?? (this.writeSide = new WriteSideStream(this)); } } }

        private sealed class WriteSideStream : StreamBase
        {
            private readonly MemoryPipe pipe;

            public WriteSideStream(MemoryPipe pipe)
                : base(StreamCapabilities.Write | StreamCapabilities.Async | StreamCapabilities.Timeout)
            {
            }

            protected override void InternalWrite(byte[] buffer, int offset, int count)
            {
                this.pipe.Write(buffer, offset, count);
            }

            protected override Task InternalWriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                return this.pipe.WriteAsync(buffer, offset, count, cancellationToken);
            }

            protected override TimeSpan InternalWriteTimeout
            {
                get { return TimeSpan.FromMilliseconds(this.pipe.WriteTimeout); }
                set { this.pipe.WriteTimeout = (int)value.TotalMilliseconds; }
            }

            protected override void InternalFlush()
            {
                this.pipe.Flush();
            }

            protected override Task InternalFlushAsync(CancellationToken cancellationToken)
            {
                return this.pipe.FlushAsync();
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    this.pipe.CloseWriteSide();
                }
                base.Dispose(disposing);
            }
        }
        #endregion
    }
}
