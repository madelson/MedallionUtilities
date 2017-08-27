using Medallion.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;

namespace Playground.ByRef
{
    public interface IByRefEnumerator<T>
    {
        ref T Current { get; }
        bool MoveNext();
    }

    public interface IByRefEnumerable<T> 
    {
        IByRefEnumerator<T> GetEnumerator();
    }

    delegate bool RefPredicate<T>(ref T value);

    public sealed class ByRefList<T> : IByRefEnumerable<T>, IEnumerable<T>
    {
        private int count;
        private T[] array = Empty.Array<T>();

        public int Count => this.count;

        public ref T this[int index]
        {
            get
            {
                if (index >= this.count) { throw new IndexOutOfRangeException(nameof(index)); }
                return ref this.array[index];
            }
        }

        public void Add(ref T value)
        {
            if (this.count == this.array.Length)
            {
                Array.Resize(ref this.array, Math.Max(this.array.Length * 2, 10));
            }

            this.array[this.count++] = value;
        }

        public Enumerator GetEnumerator() => new Enumerator(this);

        IByRefEnumerator<T> IByRefEnumerable<T>.GetEnumerator() => this.GetEnumerator();
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => this.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        public struct Enumerator : IByRefEnumerator<T>, IEnumerator<T>
        {
            private T[] array;
            private int index, count;
            
            internal Enumerator(ByRefList<T> list)
            {
                this.array = list.array;
                this.index = -1;
                this.count = list.count;
            }

            public ref T Current => ref this.array[this.index];

            T IEnumerator<T>.Current => this.array[this.index];

            object IEnumerator.Current => this.array[this.index];

            public bool MoveNext() => ++this.index < this.count;

            void IDisposable.Dispose() => this.array = null;

            void IEnumerator.Reset() => this.index = -1;
        }
    }
}
