using System;

namespace Stress.Generator
{
    public class ResourceProperty : Attribute
    {
        string Help { get; set; }
        public ResourceProperty(string help)
        {
            this.Help = help;
        }
    }

    public class OptionalResourceProperty : Attribute
    {
        string Help { get; set; }
        string Default { get; set; }
        public OptionalResourceProperty(string help, string @default)
        {
            this.Help = help;
            this.Default = @default;
        }
    }
}