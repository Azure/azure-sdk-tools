using System;
using System.Reflection;

namespace Stress.Generator
{
    public class ResourcePropertyInfo
    {
        public PropertyInfo Info;
        public IResourceProperty Property;

        public ResourcePropertyInfo(PropertyInfo info, IResourceProperty property)
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
}