using System;
using System.Reflection;

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
}