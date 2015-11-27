using Medallion;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.IO
{
    // ideas:
    // streambase, pipe
    // same stuff with readers & writers?
    // other enumerables with binary reader?
    // copyto for reader => writer

    public static class Streams
    {
        #region ---- Take ----
        public Stream Take(this Stream stream, int count, bool leaveOpen = false)
        {

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
        #endregion
    }
}
