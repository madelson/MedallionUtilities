using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.Collections
{
    public static class Empty<TElement>
    {
        private static TElement[] array;

        public static TElement[] Array
        {
            get { return array ?? (array = new TElement[0]); }
        }

        public static IEnumerable<TElement> Enumerable { get { return EmptyCollection.Instance; } }
        public static IReadOnlyCollection<TElement> Collection { get { return EmptyCollection.Instance; } }
        public static IReadOnlyList<TElement> List { get { return EmptyCollection.Instance; } }
        public static ISet<TElement> Set { get { return EmptyCollection.Instance; } }
        public static ICollection<TElement> FrozenCollection { get { return EmptyCollection.Instance; } }
        public static IList<TElement> FrozenList { get { return EmptyCollection.Instance; } }

        private sealed class EmptyCollection : IList<TElement>, IReadOnlyList<TElement>, ISet<TElement>, IEnumerator<TElement>, IList
        {
            public static readonly EmptyCollection Instance = new EmptyCollection();

            private EmptyCollection() { }

            TElement IReadOnlyList<TElement>.this[int index]
            {
                get { throw ThrowCannotIndex(); }
            }

            object IList.this[int index]
            {
                get { throw ThrowCannotIndex(); }
                set { throw ThrowReadOnly(); }
            }

            TElement IList<TElement>.this[int index]
            {
                get { throw ThrowCannotIndex(); }
                set { throw ThrowReadOnly(); }
            }

            int IReadOnlyCollection<TElement>.Count { get { return 0; } }

            int ICollection.Count { get { return 0; } }

            int ICollection<TElement>.Count { get { return 0; } }

            object IEnumerator.Current
            {
                // based on ((IEnumerator)new List<int>().GetEnumerator()).Current
                get { throw new InvalidOperationException("Enumeration has either not started or has already finished"); }
            }

            TElement IEnumerator<TElement>.Current
            {
                // based on new List<int>().GetEnumerator().Current
                get { return default(TElement); }
            }

            bool IList.IsFixedSize { get { return true; } }

            bool IList.IsReadOnly { get { return true; } }

            bool ICollection<TElement>.IsReadOnly { get { return true; } }

            bool ICollection.IsSynchronized { get { return false; } }

            object ICollection.SyncRoot { get { return this; } }

            int IList.Add(object value)
            {
                throw ThrowReadOnly();
            }

            bool ISet<TElement>.Add(TElement item)
            {
                throw ThrowReadOnly();
            }

            void ICollection<TElement>.Add(TElement item)
            {
                throw ThrowReadOnly();
            }

            void IList.Clear()
            {
                throw ThrowReadOnly();
            }

            void ICollection<TElement>.Clear()
            {
                throw ThrowReadOnly();
            }

            bool IList.Contains(object value)
            {
                return false;
            }

            bool ICollection<TElement>.Contains(TElement item)
            {
                return false;
            }

            void ICollection.CopyTo(Array array, int index)
            {
                Throw.IfNull(array, "array");
                Throw.IfOutOfRange(index, "index", min: 0, max: array.Length);
            }

            void ICollection<TElement>.CopyTo(TElement[] array, int arrayIndex)
            {
                Throw.IfNull(array, "array");
                Throw.IfOutOfRange(arrayIndex, "arrayIndex", min: 0, max: array.Length);
            }

            void IDisposable.Dispose()
            {
            }

            void ISet<TElement>.ExceptWith(IEnumerable<TElement> other)
            {
                throw ThrowReadOnly();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this;
            }

            IEnumerator<TElement> IEnumerable<TElement>.GetEnumerator()
            {
                return this;
            }

            int IList.IndexOf(object value)
            {
                return -1;
            }

            int IList<TElement>.IndexOf(TElement item)
            {
                return -1;
            }

            void IList.Insert(int index, object value)
            {
                throw ThrowReadOnly();
            }

            void IList<TElement>.Insert(int index, TElement item)
            {
                throw ThrowReadOnly();
            }

            void ISet<TElement>.IntersectWith(IEnumerable<TElement> other)
            {
                throw ThrowReadOnly();
            }

            bool ISet<TElement>.IsProperSubsetOf(IEnumerable<TElement> other)
            {
                Throw.IfNull(other, "other");
                return other.Any();
            }

            bool ISet<TElement>.IsProperSupersetOf(IEnumerable<TElement> other)
            {
                Throw.IfNull(other, "other");
                return false;
            }

            bool ISet<TElement>.IsSubsetOf(IEnumerable<TElement> other)
            {
                Throw.IfNull(other, "other");
                return true;
            }

            bool ISet<TElement>.IsSupersetOf(IEnumerable<TElement> other)
            {
                Throw.IfNull(other, "other");
                return !other.Any();
            }

            bool IEnumerator.MoveNext()
            {
                return false;
            }

            bool ISet<TElement>.Overlaps(IEnumerable<TElement> other)
            {
                return false;
            }

            void IList.Remove(object value)
            {
                throw ThrowReadOnly();
            }

            bool ICollection<TElement>.Remove(TElement item)
            {
                throw ThrowReadOnly();
            }

            void IList.RemoveAt(int index)
            {
                throw ThrowReadOnly();
            }

            void IList<TElement>.RemoveAt(int index)
            {
                throw ThrowReadOnly();
            }

            void IEnumerator.Reset()
            {
            }

            bool ISet<TElement>.SetEquals(IEnumerable<TElement> other)
            {
                Throw.IfNull(other, "other");
                return !other.Any();
            }

            void ISet<TElement>.SymmetricExceptWith(IEnumerable<TElement> other)
            {
                throw ThrowReadOnly();
            }

            void ISet<TElement>.UnionWith(IEnumerable<TElement> other)
            {
                throw ThrowReadOnly();
            }

            private static Exception ThrowCannotIndex()
            {
                throw new ArgumentOutOfRangeException("Cannot index into an empty collection");
            }

            private static Exception ThrowReadOnly([CallerMemberName] string memberName = null)
            {
                throw new InvalidOperationException(memberName + ": the collection is read-only");
            }
        }
    }
}
