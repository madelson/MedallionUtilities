using Medallion;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Medallion.IO
{
    // TODO library name ideas: DreamStream, Flow

    // ideas:
    // streambase, pipe
    // same stuff with readers & writers?
    // other enumerables with binary reader?
    // copyto for reader => writer

    public static class Streams
    {
        #region ---- Take ----
        public static Stream Take(this Stream stream, long count, bool leaveOpen = false)
        {
            Throw.IfNull(stream, nameof(stream));
            Throw.IfOutOfRange(count, nameof(count), min: 0);

            return new TakeStream(stream, count, leaveOpen);
        }

        private sealed class TakeStream : StreamBase
        {
            private readonly Stream stream;
            private readonly bool leaveOpen;
            private long remaining;

            public TakeStream(Stream stream, long count, bool leaveOpen)
                : base(StreamCapabilities.Read | StreamCapabilities.Async)
            {
                this.stream = stream;
                this.remaining = count;
                this.leaveOpen = leaveOpen;
            }

            protected override int InternalRead(byte[] buffer, int offset, int count)
            {
                if (this.remaining == 0)
                {
                    return 0; // eof
                }

                var bytesRead = this.stream.Read(buffer, offset, (int)Math.Min(count, remaining));
                if (bytesRead == 0)
                {
                    this.remaining = 0;
                }
                else
                {
                    this.remaining -= bytesRead;
                }

                return bytesRead;
            }

            protected async override Task<int> InternalReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                if (this.remaining == 0)
                {
                    return 0; // eof
                }

                var bytesRead = await this.stream.ReadAsync(buffer, offset, (int)Math.Min(count, remaining), cancellationToken).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    this.remaining = 0;
                }
                else
                {
                    this.remaining -= bytesRead;
                }

                return bytesRead;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing && !this.leaveOpen)
                {
                    this.stream.Dispose();
                }
                base.Dispose(disposing);
            }
        }
        #endregion

        #region ---- Concat ----
        public static Stream Concat(this Stream first, Stream second)
        {
            Throw.IfNull(first, nameof(first));
            Throw.IfNull(second, nameof(second));
            Throw.If(!first.CanRead, nameof(first), "must be readable");
            Throw.If(!second.CanRead, nameof(second), "must be readable");

            return new ConcatStream(first, second);
        }

        private sealed class ConcatStream : Stream
        {
            private readonly Stream first, second;
            private IReadOnlyList<Stream> cachedStreams;
            private int currentStreamIndex;

            public ConcatStream(Stream first, Stream second)
            {
                this.first = first;
                this.second = second;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                // todo arg checks

                var streams = this.GetStreams();

                while (true)
                {
                    if (this.currentStreamIndex > streams.Count)
                    {
                        return 0;
                    }

                    var stream = streams[this.currentStreamIndex];
                    var bytesRead = stream.Read(buffer, offset, count);
                    if (bytesRead == 0)
                    {
                        // done with this stream, move to the next one
                        ++this.currentStreamIndex;
                    }
                    else
                    {
                        return bytesRead;
                    }
                }
            }

            private IReadOnlyList<Stream> GetStreams()
            {
                if (this.cachedStreams == null)
                {
                    var collector = new List<Stream>();
                    GatherStreams(this, collector);
                    this.cachedStreams = collector;
                }
                return this.cachedStreams;
            }

            private static void GatherStreams(Stream stream, List<Stream> collector)
            {
                var concatStream = stream as ConcatStream;
                if (concatStream != null)
                {
                    if (concatStream.cachedStreams != null)
                    {
                        collector.AddRange(concatStream.cachedStreams);
                    }
                    else
                    {
                        GatherStreams(concatStream.first, collector);
                        GatherStreams(concatStream.second, collector);
                    }
                }
                else
                {
                    collector.Add(stream);
                }
            }
        }
        #endregion

        #region ---- Byte Enumerables ----
        public static Stream AsStream(IEnumerable<byte> bytes)
        {

        }

        public static IEnumerable<byte> AsEnumerable(this Stream stream, bool leaveOpen = false)
        {
        }
        #endregion

        #region ---- Replace ----
        public static Stream Replace(this Stream stream, IReadOnlyList<byte> sequence, IReadOnlyList<byte> replacement, bool leaveOpen = false)
        {

        }
        #endregion

        #region ---- Random ----
        public static Stream AsStream(this Random random)
        {

        } 

        public static Stream AsStream(this RandomNumberGenerator randomNumberGenerator)
        {

        }

        private sealed class RandomStream : StreamBase
        {
            private readonly Action<byte[]> nextBytes;
            private byte[] buffer;
            private int bufferSize;

            public RandomStream(Action<byte[]> nextBytes)
                : base(StreamCapabilities.Read)
            {
                this.nextBytes = nextBytes;
            }
            
            protected override int InternalRead(byte[] buffer, int offset, int count)
            {
                // fast path: fill the provided buffer directly
                if (offset == 0 && count == buffer.Length && this.bufferSize == 0)
                {
                    this.nextBytes(buffer);
                    return count;
                }

                // standard path: fill the provided buffer from our buffer
                var bytesRead = 0;
                while (bytesRead < count)
                {
                    // if we have no bytes, refill the buffer
                    if (this.bufferSize == 0)
                    {
                        if (this.buffer == null)
                        {
                            this.buffer = new byte[1024];
                        }
                        this.nextBytes(this.buffer);
                        this.bufferSize = this.buffer.Length;
                    }
                    
                    var bytesToCopy = Math.Min(count - bytesRead, this.bufferSize);
                    Buffer.BlockCopy(src: this.buffer, srcOffset: this.buffer.Length - this.bufferSize, dst: buffer, dstOffset: offset + bytesRead, count: bytesToCopy);
                    bytesRead += bytesToCopy;
                    this.bufferSize -= bytesToCopy;
                }

                return count;
            }
        }
        #endregion
    }
}
