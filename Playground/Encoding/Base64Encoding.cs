using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Playground
{
    public sealed class Base64Encoding : Encoding
    {
        public override int GetByteCount(char[] chars, int index, int count)
        {
            throw new NotImplementedException();
        }

        public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex)
        {
            throw new NotImplementedException();
        }

        public override int GetCharCount(byte[] bytes, int index, int count)
        {
            throw new NotImplementedException();
        }

        public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
        {
            throw new NotImplementedException();
        }

        public override int GetMaxByteCount(int charCount)
        {
            throw new NotImplementedException();
        }

        public override int GetMaxCharCount(int byteCount)
        {
            throw new NotImplementedException();
        }

        private sealed class Base64Encoder : Encoder
        {
            public override int GetByteCount(char[] chars, int index, int count, bool flush)
            {
                throw new NotImplementedException();
            }

            public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex, bool flush)
            {
                throw new NotImplementedException();
            }
        }

        private sealed class Base64Decoder : Decoder
        {
            public override int GetCharCount(byte[] bytes, int index, int count)
            {
                throw new NotImplementedException();
            }

            public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
            {
                return System.Convert.ToBase64CharArray(bytes, byteIndex, byteCount, chars, charIndex);
            }
        }
    }
}
