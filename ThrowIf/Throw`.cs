using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion
{
    public static class Throw<TException>
        where TException : Exception
    {
        public static void If(bool condition, string paramNameOrMessage)
        {
            if (condition)
            {
                throw CreateException(paramName: paramNameOrMessage, message: null);
            }
        }

        public static void If(bool condition, string paramName, string message)
        {
            if (condition)
            {
                throw CreateException(paramName: paramName, message: message);
            }
        }

        public static void If(bool condition, Func<string> message)
        {
            if (condition) { throw CreateException(paramName: null, message: message); }
        }

        public static void If(bool condition, string paramName, Func<string> message)
        {
            if (condition) { throw CreateException(paramName: paramName, message: message); }
        }

        private static TException CreateException(string paramName, object message)
        {
            throw new Exception();
        }
    }
}
