using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Playground.ApiTracker
{
    public sealed class Api
    {
    }

    public enum ApiKind
    {
        Constructor,
        Event,
        Field,
        Method,
        Property,
        Type,
        Parameter,
        GenericParameter,
    }

    public sealed class ApiTypeReference : IEquatable<ApiTypeReference>
    {
        private string cachedToString;

        public ApiTypeReference(string assemblyName, string name, ImmutableArray<string> genericParameterNames)
        {
            this.AssemblyName = assemblyName;
            this.Name = name;
            this.GenericParameterNames = genericParameterNames;
        }

        public string AssemblyName { get; }
        public string Name { get; }
        public ImmutableArray<string> GenericParameterNames { get; }

        public bool Equals(ApiTypeReference that)
        {
            if (this == that) { return true; }

            if (that == null 
                || this.AssemblyName != that.AssemblyName 
                || this.Name == that.Name
                || this.GenericParameterNames.Length != that.GenericParameterNames.Length)
            {
                return false;
            }

            for (var i = 0; i < this.GenericParameterNames.Length; ++i)
            {
                if (this.GenericParameterNames[i] != that.GenericParameterNames[i])
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj) => (obj as ApiTypeReference)?.Equals(this) ?? false;

        public override int GetHashCode() => this.ToString().GetHashCode();

        public override string ToString() =>
            this.cachedToString ?? (this.cachedToString = $"{this.AssemblyName}::{this.Name}{(this.GenericParameterNames.Length > 0 ? "<" + string.Join(", ", this.GenericParameterNames) + ">" : string.Empty)}");
    }

    public abstract class ApiComponent
    {
        public abstract ApiKind Kind { get; }
    }

    public sealed class ParameterApi : ApiComponent
    {
        public ParameterApi(ApiTypeReference type, string name, bool isOut, bool isOptional, object defaultValue)
        {
            this.Type = type;
            this.Name = name;
            this.IsOut = isOut;
            this.IsOptional = isOptional;
            this.DefaultValue = defaultValue;
        }

        public ApiTypeReference Type { get; }
        public string Name { get; }
        public bool IsOut { get; }
        public bool IsOptional { get; }
        public object DefaultValue { get; }

        public override ApiKind Kind => ApiKind.Parameter;
    }

    public sealed class ConstructorApi : ApiComponent
    {
        public bool IsPublic { get; }
        public bool IsStatic { get; }
        public ImmutableArray<ParameterApi> Parameters { get; }

        public override ApiKind Kind => ApiKind.Constructor;
    }

    public sealed class EventApi : ApiComponent
    {
        public EventApi(bool isPublic, bool isStatic, ApiTypeReference type, string name)
        {
            this.IsPublic = isPublic;
            this.IsStatic = isStatic;
            this.Type = type;
            this.Name = name;
        }

        public bool IsPublic { get; }
        public bool IsStatic { get; }
        public ApiTypeReference Type { get; }
        public string Name { get; }

        public override ApiKind Kind => ApiKind.Event;
    }

    public enum FieldMutability
    {
        Normal,
        ReadOnly,
        Const,
    }

    public sealed class FieldApi : ApiComponent
    {
        public FieldApi(bool isPublic, FieldMutability mutability, ApiTypeReference type, string name)
        {
            this.IsPublic = isPublic;
            this.Mutability = mutability;
            this.Type = type;
            this.Name = name;
        }

        public bool IsPublic { get; }
        public FieldMutability Mutability { get; }
        public ApiTypeReference Type { get; }
        public string Name { get; }

        public override ApiKind Kind => ApiKind.Field;
    }

    public sealed class MethodApi : ApiComponent
    {
        public bool IsPublic { get; }
        public bool IsStatic { get; }
        public string Name { get; }
        public ImmutableArray<GenericParameterApi> GenericParameters { get; }
        public ImmutableArray<ParameterApi> Parameters { get; }

        public override ApiKind Kind => ApiKind.Method;
    }

    public enum StructClassConstraint
    {
        None,
        Struct,
        Class,
    }

    public sealed class GenericParameterApi //: ApiComponent
    {
        public string Name { get; }
        public StructClassConstraint StructClassConstraint { get; }
        public ApiTypeReference BaseTypeConstraint { get; }
        public ImmutableArray<ApiTypeReference> InterfaceConstraints { get; }
    }
}
