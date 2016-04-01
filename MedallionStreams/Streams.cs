using Medallion;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Collections;

namespace Medallion.IO
{
    // TODO library name ideas: DreamStream, Flow (or other synonym e. g. creek)

        // streambase should check capabilities to avoid recursion on internal read/async
        // streambase should track active async tasks and throw on multiple
        
        // should offer version of concat that takes IEnumerable of streams
        // memory pipe should offer zero buffer option for lazy streaming with handoff

    // ideas:
    // streambase, pipe
    // same stuff with readers & writers?
    // other enumerables with binary reader?
    // copyto for reader => writer
    // lazy stream with async or sync write that consumes as a reader

    public static class Streams
    {
        private static readonly StreamCapabilities ReadCapabilities = new StreamCapabilities
        {
            CanAsyncRead = true,
            CanSyncRead = true,
            CanTimeout = true,
            CanSeek = true,
        };

        #region ---- Take ----
        public static Stream Take(this Stream stream, long count, bool leaveOpen = false)
        {
            if (stream == null) { throw new ArgumentNullException(nameof(stream)); }
            if (!stream.CanRead) { throw new ArgumentException("must be readable", nameof(stream)); }
            if (count < 0) { throw new ArgumentOutOfRangeException(nameof(count), count, "must be non-negative"); }

            return new TakeStream(stream, count, leaveOpen);
        }

        private sealed class TakeStream : StreamBase
        {
            private readonly Stream stream;
            private readonly bool leaveOpen;
            private readonly long? initialPosition;
            private readonly long count;
            private long remaining;

            public TakeStream(Stream stream, long count, bool leaveOpen)
                : base(StreamCapabilities.InferFrom(stream).Intersect(ReadCapabilities), ReadCapabilities)
            {
                this.stream = stream;
                this.count = this.remaining = count;
                this.initialPosition = stream.CanSeek ? stream.Position : default(long?);
                this.leaveOpen = leaveOpen;
            }

            protected override int InternalRead(byte[] buffer, int offset, int count)
            {
                if (this.remaining == 0)
                {
                    return 0; // eof
                }

                var bytesRead = this.stream.Read(buffer, offset, (int)Math.Min(count, this.remaining));
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

                var bytesRead = await this.stream.ReadAsync(buffer, offset, (int)Math.Min(count, this.remaining), cancellationToken).ConfigureAwait(false);
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

            protected override long InternalGetLength() => Math.Min(this.count, (this.stream.Length - this.initialPosition.Value));

            protected override long InternalGetPosition() => this.count - this.remaining;

            protected override void InternalSetPosition(long value)
            {
                if (value > this.Length) { throw new ArgumentOutOfRangeException(nameof(value), value, $"must be within the stream length ({this.Length})"); }

                this.stream.Position = this.initialPosition.Value + value;
                this.remaining = this.count - value;
            }

            protected override TimeSpan InternalReadTimeout
            {
                get { return TimeSpan.FromMilliseconds(this.stream.ReadTimeout); }
                set { this.stream.ReadTimeout = (int)value.TotalMilliseconds; }
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
            if (first == null) { throw new ArgumentNullException(nameof(first)); }
            if (second == null) { throw new ArgumentNullException(nameof(second)); }
            if (!first.CanRead) { throw new ArgumentException("must be readable", nameof(first)); }
            if (!second.CanRead) { throw new ArgumentException("must be readable", nameof(second)); }

            return new ConcatStream(first, second);
        }

        private sealed class ConcatStream : StreamBase
        {
            private readonly Stream first, second;
            private IReadOnlyList<Stream> cachedStreams;
            private int currentStreamIndex;

            public ConcatStream(Stream first, Stream second)
                : base(StreamCapabilities.InferFrom(first).Intersect(StreamCapabilities.InferFrom(second)).Intersect(ReadCapabilities), ReadCapabilities)
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

            protected override async Task<int> InternalReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                var streams = this.GetStreams();

                while (true)
                {
                    if (this.currentStreamIndex > streams.Count)
                    {
                        return 0;
                    }

                    var stream = streams[this.currentStreamIndex];
                    var bytesRead = await stream.ReadAsync(buffer, offset, count).ConfigureAwait(false);
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

            protected override long InternalGetLength() => this.GetStreams().Sum(s => s.Length);

            protected override long InternalGetPosition()
            {
                var streams = this.GetStreams();
                return checked(
                    streams.Take(this.currentStreamIndex).Sum(s => s.Length)
                        + streams[currentStreamIndex].Position
                );
            }

            protected override void InternalSetPosition(long value)
            {
                if (value > this.Length) { throw new ArgumentOutOfRangeException(nameof(value), value, $"must be within the stream length ({this.Length})"); }

                var streams = this.GetStreams();
                var currentPosition = this.Position;
                while (currentPosition != value)
                {
                    var currentStream = streams[this.currentStreamIndex];
                    var currentStreamPosition = currentStream.Position;

                    if (currentPosition > value)
                    {
                        // we need to go backwards so...
                        if (currentStreamPosition > 0)
                        {
                            // ... either move backwards as far as we can/need to within the current stream
                            var delta = Math.Min((currentPosition - value), currentStreamPosition);
                            currentStream.Position = currentStreamPosition - delta;
                            currentPosition -= delta;
                        }
                        else
                        {
                            // ... or move to the previous stream
                            --this.currentStreamIndex;
                        }
                    }
                    else
                    {
                        // we need to go forwards, so...
                        var currentStreamLength = currentStream.Length;
                        if (currentStreamPosition < currentStreamLength)
                        {
                            // ... either move forwards as far as we can/need to within the current stream
                            var delta = Math.Min((value - currentPosition), currentStreamLength - currentStreamPosition);
                            currentStream.Position = currentStreamPosition + delta;
                            currentPosition += delta;
                        }
                        else
                        {
                            // ... or moveto the next stream
                            ++this.currentStreamIndex;
                        }
                    }
                }
            }

            protected override TimeSpan InternalReadTimeout
            {
                get
                {
                    var streams = this.GetStreams();
                    var firstStreamTimeout = streams[0].ReadTimeout;
                    if (streams.Skip(1).Any(s => s.ReadTimeout != firstStreamTimeout))
                    {
                        throw new InvalidOperationException("The underlying streams have different read timeouts");
                    }
                    return TimeSpan.FromMilliseconds(firstStreamTimeout);
                }
                set
                {
                    var timeoutMillis = (int)value.TotalMilliseconds;
                    foreach (var stream in this.GetStreams()) { stream.ReadTimeout = timeoutMillis; }
                }
            }
        }
        #endregion

        #region ---- Byte Enumerables ----
        public static Stream FromEnumerator(IEnumerator<byte> bytes)
        {
            if (bytes == null) { throw new ArgumentNullException(nameof(bytes)); }

            return new ByteEnumerableStream(bytes);
        }

        private sealed class ByteEnumerableStream : StreamBase
        {
            private IEnumerator<byte> bytes;

            public ByteEnumerableStream(IEnumerator<byte> bytes)
                : base(new StreamCapabilities { CanSyncRead = true })
            {
                this.bytes = bytes;
            }

            protected override int InternalRead(byte[] buffer, int offset, int count)
            {
                for (var bytesRead = 0; bytesRead < count; ++bytesRead)
                {
                    if (!this.bytes.MoveNext())
                    {
                        return bytesRead;
                    }
                    buffer[offset + bytesRead] = this.bytes.Current;
                }

                return count;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing) { this.bytes.Dispose(); }
            }
        }

        public static IEnumerable<byte> EnumerateBytes(this Stream stream, bool leaveOpen = false)
        {
            if (stream == null) { throw new ArgumentNullException(nameof(stream)); }
            if (!stream.CanRead) { throw new ArgumentException("must be readable", nameof(stream)); }

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

        #region ---- Random ----
        public static Stream FromRandom(Random random)
        {
            if (random == null) { throw new ArgumentNullException(nameof(random)); }

            return new RandomStream(random.NextBytes);
        } 

        public static Stream FromRandomNumberGenerator(RandomNumberGenerator randomNumberGenerator)
        {
            if (randomNumberGenerator == null) { throw new ArgumentNullException(nameof(randomNumberGenerator)); }

            return new RandomStream(randomNumberGenerator.GetBytes);
        }

        private sealed class RandomStream : StreamBase
        {
            private readonly Action<byte[]> nextBytes;
            private byte[] buffer;
            private int bufferSize;

            public RandomStream(Action<byte[]> nextBytes)
                : base(new StreamCapabilities { CanSyncRead = true })
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
