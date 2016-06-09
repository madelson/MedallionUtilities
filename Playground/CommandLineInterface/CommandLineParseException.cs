using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Playground.CommandLineInterface
{
    public sealed class CommandLineParseException : Exception
    {
        internal CommandLineParseException(string message, ArraySegment<string> errorSegment, Exception innerException = null)
            : base(message, innerException)
        {
            this.ErrorSegment = errorSegment;
        }

        public ArraySegment<string> ErrorSegment { get; }
    }
}
