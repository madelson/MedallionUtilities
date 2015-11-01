using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Medallion
{
    public static class Throw
    {
        public static void If(bool condition, string paramName, string message = null)
        {
            if (condition)
            {
                throw new ArgumentException(paramName: paramName, message: FormatMessage(message));
            }
        }

        public static T IfNull<T>(T value, string paramName, string message = null)
        {
            if (value == null)
            {
                throw new ArgumentNullException(paramName: paramName, message: FormatMessage(message));
            }
            return value;
        }

        public static string IfNullOrEmpty(string value, string paramName, string message = null)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw CreateNullOrException(value, paramName, message, "Value may not be empty. ");
            }
            return value;
        }

        public static string IfNullOrWhitespace(string value, string paramName, string message = null)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw CreateNullOrException(value, paramName, message, "Value may not be empty or whitespace.");
            }
            return value;
        }

        public static T IfOutOfRange<T>(T value, string paramName, T? min = null, T? max = null, string message = null)
            where T : struct, IComparable<T>
        {
            if (min.HasValue && value.CompareTo(min.Value) < 0)
            {
                throw new ArgumentOutOfRangeException(paramName: paramName, message: FormatMessage(GetMessage(message) ?? string.Format("Value '{0}' was less than minimum allowed value '{1}'. ", value, min.Value)));
            }
            if (max.HasValue && value.CompareTo(max.Value) > 0)
            {
                throw new ArgumentOutOfRangeException(paramName: paramName, message: FormatMessage(GetMessage(message) ?? string.Format("Value '{0}' was greater than maximum allowed value '{1}'. ", value, max.Value)));
            }
            return value;
        }

        public static IReadOnlyCollection<TElement> IfNullOrEmpty<TElement>(IReadOnlyCollection<TElement> collection, string paramName, string message = null)
        {
            Throw.IfNull(collection, paramName: paramName, message: message);
            if (collection.Count == 0)
            {
                throw CreateEmptyCollectionException(paramName, message);
            }
            return collection;
        }

        public static ICollection<TElement> IfMutableCollectionNullOrEmpty<TElement>(ICollection<TElement> collection, string paramName, string message = null)
        {
            Throw.IfNull(collection, paramName: paramName, message: message);
            if (collection.Count == 0)
            {
                throw CreateEmptyCollectionException(paramName, message);
            }
            return collection;
        }

        public static TCollection IfCollectionNullOrEmpty<TCollection>(TCollection collection, string paramName, string message = null)
            where TCollection : System.Collections.ICollection
        {
            Throw.IfNull(collection, paramName: paramName, message: message);
            if (collection.Count == 0)
            {
                throw CreateEmptyCollectionException(paramName, message);
            }
            return collection;
        }

        public static NotSupportedException NotSupported(string message = null, [CallerMemberName] string memberName = null)
        {
            throw CreateNotSupportedException(memberName, message);
        }

        public static NotSupportedException NotSupported(MethodBase method, string message = null)
        {
            Throw.IfNull(method, "method");
            throw CreateNotSupportedException(method.ToString(), message);
        }

        private static Exception CreateNullOrException(string value, string paramName, object messageOrFactory, string orReason)
        {
            return value == null
                ? new ArgumentNullException(paramName: paramName, message: FormatMessage(messageOrFactory) ?? orReason)
                : new ArgumentException(paramName: paramName, message: FormatMessage(messageOrFactory));
        }

        private static Exception CreateEmptyCollectionException(string paramName, object messageOrFactory)
        {
            return new ArgumentException(paramName: paramName, message: FormatMessage(GetMessage(messageOrFactory) ?? "Collection must not be empty. "));
        }
        
        private static NotSupportedException CreateNotSupportedException(string memberName, string message)
        {
            return message == null
                ? memberName == null
                    ? new NotSupportedException()
                    : new NotSupportedException(memberName)
                : memberName == null
                    ? new NotSupportedException(message)
                    : new NotSupportedException(string.Format("{0}: {1}", memberName, message));
        }

        private static string FormatMessage(object messageOrFactory)
        {
            var message = GetMessage(messageOrFactory);
            if (string.IsNullOrEmpty(message))
            {
                return message;
            }
            switch (message[message.Length - 1])
            {
                case ' ':
                    return message;
                case '.':
                    return message + " ";
                default:
                    return message + ". ";
            }
        }

        private static string GetMessage(object messageOrFactory)
        {
            var messageFactory = messageOrFactory as Func<string>;
            return messageFactory != null ? messageFactory() : (string)messageOrFactory;
        }
    }
}
