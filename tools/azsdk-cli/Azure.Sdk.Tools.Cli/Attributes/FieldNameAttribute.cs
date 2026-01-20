namespace Azure.Sdk.Tools.Cli.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public class FieldNameAttribute : Attribute
    {
        public string Name { get; }
        public FieldNameAttribute(string name) => Name = name;
    }
}