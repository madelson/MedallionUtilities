using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.IO
{
    public struct StreamCapabilities : IEquatable<StreamCapabilities>
    {
        [Flags]
        private enum CapabilityFlags
        {
            None = 0,
            SyncRead = 1 << 0,
            AsyncRead = 1 << 1,
            SyncWrite = 1 << 2,
            AsyncWrite = 1 << 3,
            GetPosition = 1 << 4,
            GetLength = 1 << 5,
            Seek = GetPosition | GetLength | 1 << 6,
            Timeout = 1 << 7,
            All = SyncRead | AsyncRead | SyncWrite | AsyncWrite | Seek | Timeout,
        }

        private CapabilityFlags flags;

        public bool CanRead => this.CanSyncRead || this.CanAsyncRead;

        public bool CanSyncRead
        {
            get { return (this.flags & CapabilityFlags.SyncRead) != CapabilityFlags.None; }
            set { this.flags |= CapabilityFlags.SyncRead; }
        }

        public bool CanAsyncRead
        {
            get { return (this.flags & CapabilityFlags.AsyncRead) != CapabilityFlags.None; }
            set { this.flags |= CapabilityFlags.AsyncRead; }
        }

        public bool CanWrite => this.CanSyncWrite || this.CanAsyncWrite;

        public bool CanSyncWrite
        {
            get { return (this.flags & CapabilityFlags.SyncWrite) != CapabilityFlags.None; }
            set { this.flags |= CapabilityFlags.SyncWrite; }
        }

        public bool CanAsyncWrite
        {
            get { return (this.flags & CapabilityFlags.AsyncWrite) != CapabilityFlags.None; }
            set { this.flags |= CapabilityFlags.AsyncWrite; }
        }

        public bool CanGetPosition
        {
            get { return (this.flags & CapabilityFlags.GetPosition) != CapabilityFlags.None; }
            set { this.flags |= CapabilityFlags.GetPosition; }
        }

        public bool CanGetLength
        {
            get { return (this.flags & CapabilityFlags.GetLength) != CapabilityFlags.None; }
            set { this.flags |= CapabilityFlags.GetLength; }
        }

        public bool CanSeek
        {
            get { return (this.flags & CapabilityFlags.Seek) == CapabilityFlags.Seek; }
            set { this.flags |= CapabilityFlags.Seek; }
        }

        public bool CanTimeout
        {
            get { return (this.flags & CapabilityFlags.Timeout) != CapabilityFlags.None; }
            set { this.flags |= CapabilityFlags.Timeout; }
        }

        public static StreamCapabilities None => default(StreamCapabilities);
        public static StreamCapabilities All => new StreamCapabilities { flags = CapabilityFlags.All };

        public StreamCapabilities Union(StreamCapabilities that) => new StreamCapabilities { flags = this.flags | that.flags };

        public StreamCapabilities Intersect(StreamCapabilities that) => new StreamCapabilities { flags = this.flags & that.flags };

        public StreamCapabilities Except(StreamCapabilities that) => new StreamCapabilities { flags = this.flags & ~that.flags };

        public static StreamCapabilities InferFrom(Stream stream)
        {
            if (stream == null) { throw new ArgumentNullException(nameof(stream)); }

            return new StreamCapabilities
            {
                CanAsyncRead = stream.CanRead,
                CanAsyncWrite = stream.CanWrite,
                CanSeek = stream.CanSeek,
                CanSyncRead = stream.CanRead,
                CanSyncWrite = stream.CanWrite,
                CanTimeout = stream.CanTimeout,
            };
        }

        public bool Equals(StreamCapabilities other)
        {
            return this.flags == other.flags;
        }

        public override bool Equals(object thatObj)
        {
            var that = thatObj as StreamCapabilities?;
            return that.HasValue && this.Equals(that.Value);
        }

        public override int GetHashCode()
        {
            return EqualityComparer<CapabilityFlags>.Default.GetHashCode(this.flags);
        }

        public static bool operator ==(StreamCapabilities @this, StreamCapabilities that)
        {
            return @this.Equals(that);
        }

        public static bool operator !=(StreamCapabilities @this, StreamCapabilities that)
        {
            return !(@this == that);
        }

        public override string ToString() => this.flags.ToString();
    }
}
