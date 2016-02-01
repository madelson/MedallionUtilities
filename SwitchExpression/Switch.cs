using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;

namespace Medallion
{
    public static class SwitchExpressions
    {
        #region ---- API Methods ----
        public static SwitchExpression<TSwitch> Switch<TSwitch>(TSwitch on, IEqualityComparer<TSwitch> comparer = null)
        {
            return new SwitchExpression<TSwitch>(on, comparer ?? EqualityComparer<TSwitch>.Default);
        }

        public static SwitchExpression<TSwitch, TResult> Case<TSwitch, TResult>(TSwitch matches, TResult value)
        {
            return SwitchExpression<TSwitch, TResult>.CreateCase(matches, value);
        }

        public static SwitchExpression<TSwitch, TResult> Case<TSwitch, TResult>(TSwitch matches, Func<TSwitch, TResult> valueFactory)
        {
            if (valueFactory == null) { throw new ArgumentNullException(nameof(valueFactory)); }

            return SwitchExpression<TSwitch, TResult>.CreateCase(matches, valueFactory);
        }

        public static TypeCaseSwitchExpression<TResult> Case<TType, TResult>(Func<TType, TResult> valueFactory)
        {
            if (valueFactory == null) { throw new ArgumentNullException(nameof(valueFactory)); }

            return new TypeCaseSwitchExpression<TType, TResult>(valueFactory);
        }

        public static CompletedSwitchExpression<TResult> Default<TResult>(TResult result) => new CompletedSwitchExpression<TResult>(result);

        public static DefaultThrowExpression DefaultThrow(Exception exception = null)
        {
            var exceptionToThrow = exception ?? new InvalidOperationException("switch expression did not match");
            if (exceptionToThrow.StackTrace != null)
            {
                ExceptionDispatchInfo.Capture(exceptionToThrow).Throw();
            }
            throw exceptionToThrow;
        }
        #endregion

        #region ---- Helper Types ----
        public struct SwitchExpression<TSwitch>
        {
            internal SwitchExpression(TSwitch on, IEqualityComparer<TSwitch> comparer)
            {
                this.On = on;
                this.Comparer = comparer;
            }

            internal TSwitch On { get; private set; }
            internal IEqualityComparer<TSwitch> Comparer { get; private set; }

            public SwitchExpression<TSwitch, TResult> WithResultType<TResult>(TResult schema = default(TResult))
            {
                return this;
            }
        }

        public struct SwitchExpression<TSwitch, TResult>
        {
            private TSwitch switchValue;
            private object objectState;
            private TResult value;
            private State state;
            
            public TResult Value
            {
                get
                {
                    switch (this.state)
                    {
                        case State.Completed:
                            return this.value;
                        case State.AwaitingMatch:
                            throw new InvalidOperationException("the switch has not yet matched");
                        default:
                            throw new InvalidOperationException("this switch must be used as part of an || chain and cannot be used to start such a chain");
                    }
                }
            }

            internal static SwitchExpression<TSwitch, TResult> CreateCase(TSwitch matches, TResult value)
            {
                return new SwitchExpression<TSwitch, TResult> { switchValue = matches, value = value, state = State.Case };
            }

            internal static SwitchExpression<TSwitch, TResult> CreateCase(TSwitch matches, Func<TSwitch, TResult> valueFactory)
            {
                return new SwitchExpression<TSwitch, TResult> { switchValue = matches, objectState = valueFactory, state = State.CaseWithValueFactory };
            }

            internal static SwitchExpression<TSwitch, TResult> CreateCase(TypeCaseSwitchExpression<TResult> @switch)
            {
                return new SwitchExpression<TSwitch, TResult> { objectState = @switch, state = State.TypeCase };
            }

            public static implicit operator SwitchExpression<TSwitch, TResult>(SwitchExpression<TSwitch> @switch)
            {
                return new SwitchExpression<TSwitch, TResult> { switchValue = @switch.On, objectState = @switch.Comparer, state = State.AwaitingMatch };
            }

            public static implicit operator SwitchExpression<TSwitch, TResult>(TypeCaseSwitchExpression<TResult> @switch)
            {
                if (@switch == null) { throw new InvalidOperationException(nameof(@switch)); }

                return CreateCase(@switch);
            }

            public static implicit operator SwitchExpression<TSwitch, TResult>(DefaultThrowExpression @switch)
            {
                throw new InvalidOperationException($"{typeof(DefaultThrowExpression).Name}s should be constructed via the factory method and not used directly");
            }

            public static implicit operator CompletedSwitchExpression<TResult>(SwitchExpression<TSwitch, TResult> @switch)
            {
                return @switch.state == State.Completed
                    ? new CompletedSwitchExpression<TResult>(@switch.value)
                    : default(CompletedSwitchExpression<TResult>);
            }

            public static bool operator true(SwitchExpression<TSwitch, TResult> switchState) => switchState.state == State.Completed;
            public static bool operator false(SwitchExpression<TSwitch, TResult> switchState) => switchState.state != State.Completed;

            public static SwitchExpression<TSwitch, TResult> operator |(SwitchExpression<TSwitch, TResult> first, SwitchExpression<TSwitch, TResult> second)
            {
                if (first.state != State.AwaitingMatch) { throw new InvalidOperationException("use ||, not | to combine switch cases"); }
                
                switch (second.state)
                {
                    case State.Completed: return second;
                    case State.Case:
                        return ((IEqualityComparer<TSwitch>)first.objectState).Equals(first.switchValue, second.switchValue)
                            ? new SwitchExpression<TSwitch, TResult> { value = second.value, state = State.Completed }
                            : first; // still not matched
                    case State.CaseWithValueFactory:
                        return ((IEqualityComparer<TSwitch>)first.objectState).Equals(first.switchValue, second.switchValue)
                            ? new SwitchExpression<TSwitch, TResult> { value = ((Func<TSwitch, TResult>)second.objectState)(first.switchValue), state = State.Completed }
                            : first; // still not matched
                    case State.TypeCase:
                        TResult typeCaseResult;
                        return ((TypeCaseSwitchExpression<TResult>)second.objectState).TryGetResult(first.switchValue, out typeCaseResult)
                            ? new SwitchExpression<TSwitch, TResult> { value = typeCaseResult, state = State.Completed }
                            : first; // still not matched
                    default:
                        throw new InvalidOperationException("right-hand side of || is not a valid case");
                }
            }

            private enum State : byte
            {
                None,
                Case,
                CaseWithValueFactory,
                TypeCase,
                AwaitingMatch,
                Completed,
            }
        }

        public abstract class TypeCaseSwitchExpression<TResult>
        {
            internal abstract bool TryGetResult<TSwitch>(TSwitch switchValue, out TResult value);
        }

        private sealed class TypeCaseSwitchExpression<TType, TResult> : TypeCaseSwitchExpression<TResult>
        {
            private readonly Func<TType, TResult> resultFactory;

            public TypeCaseSwitchExpression(Func<TType, TResult> resultFactory)
            {
                this.resultFactory = resultFactory;
            }

            internal override bool TryGetResult<TSwitch>(TSwitch switchValue, out TResult value)
            {
                if (switchValue is TType)
                {
                    value = this.resultFactory((TType)(object)switchValue);
                    return true;
                }

                value = default(TResult);
                return false;
            }
        }

        public struct CompletedSwitchExpression<TResult>
        {
            private TResult value;
            private bool completed;

            internal CompletedSwitchExpression(TResult value)
            {
                this.value = value;
                this.completed = true;
            }
            
            public TResult Value
            {
                get
                {
                    if (!this.completed) { throw new InvalidOperationException("the switch has not completed"); }
                    return this.value;
                }
            }

            public static implicit operator TResult(CompletedSwitchExpression<TResult> @switch) => @switch.Value;

            public static bool operator true(CompletedSwitchExpression<TResult> @switch) => @switch.completed;
            public static bool operator false(CompletedSwitchExpression<TResult> @switch) => !@switch.completed;

            public static CompletedSwitchExpression<TResult> operator |(CompletedSwitchExpression<TResult> first, CompletedSwitchExpression<TResult> second)
            {
                if (first.completed) { throw new InvalidOperationException("use ||, not | to combine switch cases"); }
                return second;
            }
        }

        public struct DefaultThrowExpression { }
        #endregion
    }
}
