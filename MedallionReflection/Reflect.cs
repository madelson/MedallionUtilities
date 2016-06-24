using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.Reflection
{
    public static partial class Reflect
    {
        #region ---- Generic Type Search ----
        public static bool IsGenericOfType(this Type @this, Type genericTypeDefinition)
        {
            return @this.FindGenericTypeDefinition(genericTypeDefinition) != null;
        }

        public static Type[] GetGenericArguments(this Type @this, Type genericTypeDefinition)
        {
            var locatedTypeDefinition = @this.FindGenericTypeDefinition(genericTypeDefinition);
            return locatedTypeDefinition == null
                ? Type.EmptyTypes // TODO do we want this behavior?
                : locatedTypeDefinition.GetGenericArguments();
        }

        private static Type FindGenericTypeDefinition(this Type @this, Type genericTypeDefinition)
        {
            Throw.IfNull(@this, "this");
            Throw.IfNull(genericTypeDefinition, "genericTypeDefinition");
            Throw.If(!genericTypeDefinition.IsGenericTypeDefinition, "genericTypeDefinition", "must be a generic type definition (e. g. typeof(List<>))");

            if (genericTypeDefinition.IsInterface)
            {
                return @this.GetInterfaces().FirstOrDefault(
                    i => i == genericTypeDefinition 
                        || (i.IsGenericType && i.GetGenericTypeDefinition() == genericTypeDefinition)
                );
            }

            for (var type = @this; type != null; type = type.BaseType)
            {
                if (type == genericTypeDefinition
                    || (type.IsGenericType && type.GetGenericTypeDefinition() == genericTypeDefinition))
                {
                    return type;
                }
            }

            return null;
        }
        #endregion

        #region ---- CanBeNull ----
        public static bool CanBeNull(this Type type)
        {
            Throw.IfNull(type, "type");

            return !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
        }
        #endregion

        #region ---- GetDefaultValue ----
        public static object GetDefaultValue(this Type type)
        {
            Throw.IfNull(type, "type");

            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }
        #endregion

        //#region ---- MemberEquals ----
        //public static bool MemberEquals(this MemberInfo @this, MemberInfo that)
        //{
        //    if (@this == that)
        //    {
        //        return true;
        //    }
        //    if (@this == null || that == null)
        //    {
        //        return false;
        //    }
        //    if (@this.MemberType != that.MemberType || @this.MetadataToken != that.MetadataToken || @this.Module != that.Module)
        //    {
        //        return false;
        //    }
        //}
        //#endregion

        #region ---- Expression Reflection ----
        public static MethodInfo GetMethod(Expression<Action> methodCallExpression)
        {
            LambdaExpression lambda = methodCallExpression;
            return GetMethod(lambda);
        }

        public static MethodInfo GetMethod<TInstance>(Expression<Action<TInstance>> methodCallExpression)
        {
            LambdaExpression lambda = methodCallExpression;
            return GetMethod(lambda);
        }

        private static MethodInfo GetMethod(LambdaExpression methodCallExpression)
        {
            Throw.IfNull(methodCallExpression, "methodCallExpression");
            var methodCall = methodCallExpression.Body as MethodCallExpression;
            if (methodCall == null)
            {
                throw new ArgumentException("methodCallExpression: the body of the lambda expression must be a method call. Found: " + methodCallExpression.Body.NodeType);
            }

            return methodCall.Method;
        }

        public static PropertyInfo GetProperty<TProperty>(Expression<Func<TProperty>> propertyExpression)
        {
            LambdaExpression lambda = propertyExpression;
            return GetProperty(lambda);
        }

        public static PropertyInfo GetProperty<TInstance, TProperty>(Expression<Func<TInstance, TProperty>> propertyExpression)
        {
            LambdaExpression lambda = propertyExpression;
            return GetProperty(lambda);
        } 

        private static PropertyInfo GetProperty(LambdaExpression propertyExpression)
        {
            Throw.IfNull(propertyExpression, "propertyExpression");
            var property = propertyExpression.Body as MemberExpression;
            if (property == null || property.Member.MemberType != MemberTypes.Property)
            {
                throw new ArgumentException("propertyExpression: the body of the lambda expression must be a property access. Found: " + propertyExpression.Body.NodeType);
            }

            return (PropertyInfo)property.Member;
        }
        #endregion

        #region ---- GetDelegateType ----
        public static Type GetDelegateType(this MethodInfo method)
        {
            if (method == null) { throw new ArgumentNullException(nameof(method)); }

            var parameters = method.GetParameters();
            var parameterExpressions = new ParameterExpression[parameters.Length];
            for (var i = 0; i < parameters.Length; ++i)
            {
                parameterExpressions[i] = Expression.Parameter(parameters[i].ParameterType);
            }

            var lambda = Expression.Lambda(
                Expression.Call(
                    method.IsStatic ? null : Expression.Parameter(method.DeclaringType),
                    method,
                    parameterExpressions
                ),
                parameterExpressions
            );
            return lambda.Type;
        }
        #endregion

        #region ---- InvokeAndUnwrapException ----
        public static object InvokeAndUnwrapException(this MethodInfo method, object obj, params object[] parameters)
        {
            if (method == null) { throw new ArgumentNullException(nameof(method)); }

            throw new NotImplementedException();
            //return InternalInvokeAndUnwrapException(method, (m, o, p) => m.Invoke(o, p), obj, parameters);
        }

        public static object InvokeAndUnwrapException(this ConstructorInfo constructor, params object[] parameters)
        {
            if (constructor == null) { throw new ArgumentNullException(nameof(constructor)); }

            throw new NotImplementedException();
            //return InternalInvokeAndUnwrapException(constructor, (c, _, __, p) => c.Invoke(p), null, parameters);
        }

        public static object GetValueAndUnwrapException(this PropertyInfo property, object obj)
        {
            return GetValueAndUnwrapException(property, obj, index: default(object[]));
        }

        public static object GetValueAndUnwrapException(this PropertyInfo property, object obj, params object[] index)
        {
            if (property == null) { throw new ArgumentNullException(nameof(property)); }

            throw new NotImplementedException();
            //return InternalInvokeAndUnwrapException(property, (p, o, i) => p.GetValue(o, i), obj, index);
        }

        public static void SetValueAndUnwrapException(this PropertyInfo property, object obj, object value)
        {

        }

        public static void SetValueAndUnwrapException(this PropertyInfo property, object obj, object value, object[] index)
        {
            if (property == null) { throw new ArgumentNullException(nameof(property)); }

            throw new NotImplementedException();
            //return InternalInvokeAndUnwrapException(property, (p, o, i, v) => p.SetValue(o, i, v), obj, value, index);
        } 

        private static object InternalInvokeAndUnwrapException<TMember>(TMember member, Func<TMember, object, object[], object, object> invoke, object obj, object value, object[] parameters)
            where TMember : MemberInfo
        {
            try
            {
                throw new NotImplementedException();
                //return invoke(member, obj, value, parameters);
            }
            catch (TargetInvocationException ex)
            {
                if (ex.InnerException != null)
                {
                    ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                }
                throw;
            }
        }
        #endregion

        // TODO invoke with original exception
    }
}
