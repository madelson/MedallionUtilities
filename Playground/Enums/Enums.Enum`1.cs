using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.Enums
{
    public static partial class Enums
    {
        private static class Enum<TEnum> where TEnum : struct
        {
            // values
            // minvalue
            // maxvalue
            // isflags
            // hasflag
            // flagvalues
            // getflags
            // isdefined, assertisdefined
            // parsename
            // tostring

            static Enum()
            {
                if (!typeof(TEnum).IsEnum)
                {
                    throw new InvalidOperationException(typeof(TEnum) + " is not an enum type");
                }
            }

            #region ---- Flags ----
            #region ---- IsFlags ----
            private static int isFlags;

            public static bool IsFlags
            {
                get
                {
                    switch (isFlags)
                    {
                        case -1: return false;
                        case 1: return true;
                        case 0:
                            var hasFlagsAttribute = typeof(TEnum).IsDefined(typeof(FlagsAttribute), inherit: false);
                            isFlags = hasFlagsAttribute ? 1 : -1;
                            return hasFlagsAttribute;
                        default:
                            throw new InvalidOperationException("Should never get here");
                    }
                }
            }
            #endregion

            #region ---- AssertIsFlags ----
            public static void AssertIsFlags()
            {
                if (!IsFlags)
                {
                    throw new InvalidOperationException(typeof(TEnum) + " is not a flags enum");
                }
            }
            #endregion

            #region ---- HasFlag ----
            private static Func<TEnum, TEnum, bool> hasFlag;

            public static bool HasFlag(TEnum value, TEnum flag)
            {
                // todo use native at first

                if (hasFlag == null)
                {
                    var valueParameter = Expression.Parameter(typeof(TEnum));
                    var flagParameter = Expression.Parameter(typeof(TEnum));
                    var hasFlagLambda = Expression.Lambda<Func<TEnum, TEnum, bool>>(
                        body: Expression.Equal(
                            left: Expression.And(valueParameter, flagParameter),
                            right: flagParameter
                        ),
                        parameters: new[] { valueParameter, flagParameter }
                    );
                    hasFlag = hasFlagLambda.Compile();
                }

                return hasFlag(value, flag);
            }
            #endregion

            #region ---- FlagValues ----
            private static ValuesCollection flagValues;

            //public static ValuesCollection Flags
            //{

            //}
            #endregion
            #endregion

            #region ---- ToString ----
            private static Func<TEnum, string> toString;

            public static string ToString(TEnum value)
            {
                // todo fall back to native for awhile

                if (toString == null)
                {
                    var parameter = Expression.Parameter(typeof(TEnum));
                    var toStringLambda = Expression.Lambda<Func<TEnum, string>>(
                        body: Expression.Switch(
                            switchValue: parameter,
                            // todo could further optimize flags here
                            // todo doesn't actually work for flags here
                            defaultBody: Expression.Constant(null, typeof(string)),
                            cases: Values.Select(
                                    v => Expression.SwitchCase(
                                        body: Expression.Constant(v.ToString()),
                                        testValues: Expression.Constant(v)
                                    )
                                )
                                .ToArray()
                        ),
                        parameters: parameter
                    );
                    toString = toStringLambda.Compile();
                }

                return toString(value) ?? value.ToString();
            }
            #endregion

            #region ---- Values ----
            private static ValuesCollection values;

            public static ValuesCollection Values => values ?? (values = new ValuesCollection());

            public sealed class ValuesCollection : IReadOnlyList<TEnum>, IList<TEnum>
            {
                private readonly TEnum[] values;

                internal ValuesCollection()
                {
                    this.values = (TEnum[])Enum.GetValues(typeof(TEnum));
                }

                public TEnum this[int index] => this.values[index];

                TEnum IList<TEnum>.this[int index]
                {
                    get { return this[index]; }
                    set { throw ReadOnly(); }
                }

                public int Count => this.values.Length;

                public bool IsReadOnly => true;

                void ICollection<TEnum>.Add(TEnum item)
                {
                    throw ReadOnly();
                }

                void ICollection<TEnum>.Clear()
                {
                    throw ReadOnly();
                }

                public bool Contains(TEnum item)
                {
                    // todo optimize?
                    return Array.IndexOf(this.values, item) >= 0;
                }

                void ICollection<TEnum>.CopyTo(TEnum[] array, int arrayIndex) => this.values.CopyTo(array, arrayIndex);

                public Enumerator GetEnumerator()
                {
                    return new Enumerator(this);
                }

                IEnumerator<TEnum> IEnumerable<TEnum>.GetEnumerator()
                {
                    return this.GetEnumerator();
                }

                public int IndexOf(TEnum item)
                {
                    return Array.IndexOf(this.values, item);
                }

                void IList<TEnum>.Insert(int index, TEnum item)
                {
                    throw ReadOnly();
                }

                bool ICollection<TEnum>.Remove(TEnum item)
                {
                    throw ReadOnly();
                }

                void IList<TEnum>.RemoveAt(int index)
                {
                    throw ReadOnly();
                }

                IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

                private static NotSupportedException ReadOnly([CallerMemberName] string memberName = null)
                {
                    throw new NotSupportedException(memberName + " is not supported: the collection is read-only");
                }

                public struct Enumerator : IEnumerator<TEnum>
                {
                    private TEnum[] values;
                    private int index;

                    internal Enumerator(ValuesCollection values)
                    {
                        this.values = values.values;
                        this.index = -1;
                    }

                    public TEnum Current => this.values[index];

                    object IEnumerator.Current => this.Current;

                    public void Dispose() { }

                    public bool MoveNext()
                    {
                        if (this.index == this.values.Length - 1)
                        {
                            return false;
                        }

                        ++this.index;
                        return true;
                    }

                    void IEnumerator.Reset() => this.index = -1;
                }
            }
            #endregion

            private static Func<TEnum, ulong> toUInt64;

            private static ulong ToUInt64(TEnum value)
            {
                if (toUInt64 == null)
                {
                    var parameter = Expression.Parameter(typeof(TEnum));
                    toUInt64 = Expression.Lambda<Func<TEnum, ulong>>(Expression.Convert(parameter, typeof(ulong)), parameter).Compile();
                }
                return toUInt64(value);
            }
        }
    }
}
