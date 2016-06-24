using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.Collections
{
    interface IOption : IEnumerable
    {
        Type ValueType { get; }
        bool HasValue { get; }
        object Value { get; }
        // todo deep stuff
    }

    interface IOption<out T> : IOption, IEnumerable<T>
    {
        new T Value { get; }
    }

    public struct Option<T> : IOption<T>, IEquatable<Option<T>>
    {
        // cached for performance
        private static readonly EqualityComparer<T> ValueComparer = EqualityComparer<T>.Default;
        private static readonly bool IsValueType = typeof(T).IsValueType;

        private bool hasValue;
        private T value;

        public static Option<T> None { get { return default(Option<T>); } }

        public Option(T value) 
        {
            // IsValueType check prevents boxing when T is a value type. We also want new Option<string>(null) to be
            // a null option, while new Option<int?>(null) is a deep option... TODO decide if that's right
            this.hasValue = IsValueType || value != null;
            this.value = value;
        }

        public bool HasValue { get { return this.hasValue; } }
        public T Value
        {
            get
            {
                if (!this.hasValue)
                {
                    throw new InvalidOperationException("Option does not have a value");
                }
                return this.value;
            }
        }

        object IOption.Value { get { return this.Value; } }

        Type IOption.ValueType { get { return typeof(T); } }

        #region ---- Equality ----
        public bool Equals(Option<T> that)
        {
            if (this.hasValue != that.hasValue) { return false; }
            return !this.hasValue || ValueComparer.Equals(this.value, that.value);
        }

        public override bool Equals(object thatObj)
        {
            // based on Nullable<T>.Equals
            if (!this.hasValue) { return thatObj == null; }
            if (thatObj == null) { return false; }
            return this.value.Equals(thatObj);
        }

        public override int GetHashCode()
        {
            return this.hasValue ? this.value.GetHashCode() : 0;
        }

        public static bool operator ==(Option<T> @this, Option<T> that)
        {
            return @this.Equals(that);
        }

        public static bool operator !=(Option<T> @this, Option<T> that)
        {
            return !@this.Equals(that);
        }
        #endregion

        #region ---- Enumerator ----
        public Enumerator GetEnumerator() { return this.hasValue ? new Enumerator(this.value) : new Enumerator(); }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.hasValue ? new Enumerator(this.value) : Enumerator.Empty;
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return this.hasValue ? new Enumerator(this.value) : Enumerator.Empty;
        }

        public struct Enumerator : IEnumerator<T>
        {
            internal static readonly IEnumerator<T> Empty = default(Enumerator);

            private const byte EmptyState = 0, NotYetStartedState = 1, StartedState = 2, FinishedState = 3;

            private T value;
            private byte state;

            internal Enumerator(T value)
            {
                this.value = value;
                this.state = NotYetStartedState; 
            }

            object IEnumerator.Current
            {
                get
                {
                    if (this.state != StartedState)
                    {
                        throw new InvalidOperationException("The enumeration has not started or has already finished");
                    }
                    return this.value;
                }
            }

            T IEnumerator<T>.Current
            {
                get { return this.state == StartedState ? this.value : default(T); }
            }

            [EditorBrowsable(EditorBrowsableState.Never)]
            public void Dispose()
            {
                // no-op (exposed as non-explicit to prevent boxing)
            }

            public bool MoveNext()
            {
                switch (this.state)
                {
                    case NotYetStartedState:
                        this.state = StartedState;
                        return true;
                    case StartedState:
                        this.state = FinishedState;
                        return false;
                    default:
                        return false;
                }
            }

            void IEnumerator.Reset()
            {
                if (this.state != EmptyState)
                {
                    this.state = NotYetStartedState;
                }
            }
        }
        #endregion

        #region ---- Conversions ----
        public static implicit operator Option<T>(T value)
        {
            return new Option<T>(value);
        }
        #endregion
    }

    public static class Option
    {
        public static Option<T> ToOption<T>(this T? nullable) where T : struct
        {
            return nullable.HasValue ? new Option<T>(nullable.Value) : default(Option<T>);
        }

        public static T? ToNullable<T>(this Option<T> option) where T : struct
        {
            return option.HasValue ? option.Value : default(T?);
        }

        public static Option<T> Create<T>(T value)
        {
            return new Option<T>(value);
        }
    }
}
