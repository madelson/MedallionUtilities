using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Playground
{
    public sealed class HexEncoding : Encoding
    {
        public override int GetByteCount(char[] chars, int index, int count)
        {
            return this.GetMaxByteCount(count);
        }

        public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex)
        {
            throw new NotImplementedException();
        }

        public override int GetCharCount(byte[] bytes, int index, int count)
        {
            return this.GetMaxCharCount(count);
        }

        public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
        {
            throw new NotImplementedException();
        }

        public override int GetMaxByteCount(int charCount)
        {
            if (charCount < 0) { throw new ArgumentOutOfRangeException(nameof(charCount)); }

            return (charCount / 2) + (charCount % 2);
        }

        public override int GetMaxCharCount(int byteCount)
        {
            if (byteCount < 0) { throw new ArgumentOutOfRangeException(nameof(byteCount)); }

            return 2 * byteCount;
        }

        private sealed class HexEncoder : Encoder
        {
            public override int GetByteCount(char[] chars, int index, int count, bool flush)
            {
                throw new NotImplementedException();
            }

            public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex, bool flush)
            {
                throw new NotImplementedException();
            }

            public override void Reset()
            {
                base.Reset();
            }
        }

        private sealed class HexDecoder : Decoder
        {
            public override int GetCharCount(byte[] bytes, int index, int count)
            {
                throw new NotImplementedException();
            }

            public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
            {
                throw new NotImplementedException();
            }
        }
    }
}
