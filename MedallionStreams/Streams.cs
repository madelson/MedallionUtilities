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
            Throw.If(!stream.CanRead, nameof(stream) + " must be readable");
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

        private sealed class ConcatStream : StreamBase
        {
            private readonly Stream first, second;
            private IReadOnlyList<Stream> cachedStreams;
            private int currentStreamIndex;

            public ConcatStream(Stream first, Stream second)
                : base(StreamCapabilities.Read)
            {
                this.first = first;
                this.second = second;
            }

            protected override int InternalRead(byte[] buffer, int offset, int count)
            {
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
            Throw.IfNull(bytes, nameof(bytes));

            return new ByteEnumerableStream(bytes);
        }

        private sealed class ByteEnumerableStream : StreamBase
        {
            private readonly IEnumerable<byte> bytes;
            private IEnumerator<byte> enumerator;

            public ByteEnumerableStream(IEnumerable<byte> bytes)
                : base(StreamCapabilities.Read)
            {
                this.bytes = bytes;
            }

            protected override int InternalRead(byte[] buffer, int offset, int count)
            {
                var enumerator = this.enumerator ?? (this.enumerator = this.bytes.GetEnumerator());
                
                for (var bytesRead = 0; bytesRead < count; ++bytesRead)
                {
                    if (!enumerator.MoveNext())
                    {
                        return bytesRead;
                    }
                    buffer[offset + bytesRead] = enumerator.Current;
                }

                return count;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing && this.enumerator != null)
                {
                    this.enumerator.Dispose();
                }
            }
        }

        public static IEnumerable<byte> AsEnumerable(this Stream stream, bool leaveOpen = false)
        {
            Throw.IfNull(stream, nameof(stream));

            return ByteIterator(stream, leaveOpen);
        }

        private static IEnumerable<byte> ByteIterator(Stream stream, bool leaveOpen)
        {
            using (leaveOpen ? null : stream)
            {
                var buffer = new byte[1024];
                int bytesRead;
                while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    for (var i = 0; i < bytesRead; ++i)
                    {
                        yield return buffer[i];
                    }
                }
            }
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
            Throw.IfNull(random, nameof(random));

            return new RandomStream(random.NextBytes);
        } 

        public static Stream AsStream(this RandomNumberGenerator randomNumberGenerator)
        {
            Throw.IfNull(randomNumberGenerator, nameof(randomNumberGenerator));

            return new RandomStream(randomNumberGenerator.GetBytes);
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
