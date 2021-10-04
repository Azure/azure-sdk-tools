using System;
using System.Reflection;
using System.Collections.Generic;

namespace Stress.Generator
{
    public class ResourcePropertyInfo<T>
    {
        public PropertyInfo Info;
        public T Property;

        public ResourcePropertyInfo(PropertyInfo info, T property)
        {
            Info = info;
            Property = property;
        }
    }

    public interface IResourceProperty
    {
        public string Help { get; set; }
    }

    public abstract class BaseResourceProperty : Attribute, IResourceProperty
    {
        public string Help { get; set; }

        public BaseResourceProperty(string help)
        {
            this.Help = help;
        }
    }

    public class ResourceProperty : BaseResourceProperty
    {
        public ResourceProperty(string help) : base(help) {}
    }

    public class OptionalResourceProperty : BaseResourceProperty
    {
        public OptionalResourceProperty(string help) : base(help) {}
    }

    public class NestedResourceProperty : BaseResourceProperty
    {
        public Type[] Types { get; set; }

        public NestedResourceProperty(string help, Type[] types) : base(help)
        {
            Types = types;
            foreach (var t in Types)
            {
                // TODO: is there a way to compile check for this?
                if (!(t is IResource))
                {
                    throw new Exception("NestedResourceProperty type array must implement IResource");
                }
            }
        }
    }

    public class OptionalNestedResourceProperty : BaseResourceProperty
    {
        public Type[] Types { get; set; }

        public NestedResourceProperty AsNestedResourceProperty()
        {
            return new NestedResourceProperty(Help, Types);
        }

        public OptionalNestedResourceProperty(string help, Type[] types) : base(help)
        {
            Types = types;
        }
    }
}