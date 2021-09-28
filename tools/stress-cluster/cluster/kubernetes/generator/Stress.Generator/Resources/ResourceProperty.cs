using System;
using System.Reflection;

namespace Stress.Generator
{
    public class ResourcePropertyInfo
    {
        public PropertyInfo Prop;
        public string Help;

        public ResourcePropertyInfo(PropertyInfo prop, string help)
        {
            Prop = prop;
            Help = help;
        }
    }

    public class ResourceProperty : Attribute
    {
        public string Help { get; set; }

        public ResourceProperty(string help)
        {
            this.Help = help;
        }
    }

    public class OptionalResourceProperty : Attribute
    {
        public string Help { get; set; }

        public OptionalResourceProperty(string help)
        {
            this.Help = help;
        }
    }
}