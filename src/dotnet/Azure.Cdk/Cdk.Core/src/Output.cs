namespace Cdk.Core
{
    public class Output
    {
        public string Name { get; }
        public string Value { get; }
        public bool IsLiteral { get; }
        public bool IsSecure { get; }
        internal Resource Source { get; }

        public Output(string name, string value, Resource source, bool isLiteral = false, bool isSecure = false)
        {
            Name = name;
            Value = value;
            IsLiteral = isLiteral;
            IsSecure = isSecure;
            Source = source;
        }
    }
}
