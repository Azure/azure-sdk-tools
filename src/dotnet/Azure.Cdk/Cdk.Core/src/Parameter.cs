namespace Cdk.Core
{
    public readonly struct Parameter
    {
        public string Name { get; }
        public string? Description { get; }
        public object? DefaultValue { get; }
        public bool IsSecure { get; }
        internal bool IsFromOutput { get; }
        internal bool IsLiteral { get; }
        internal string? Value { get; }

        internal Parameter(Output output)
        {
            Name = output.Name;
            IsSecure = output.IsSecure;
            IsFromOutput = true;
            IsLiteral = output.IsLiteral;
            Value = output.Value;
        }

        public Parameter(string name, string? description = default, object? defaultValue = default, bool isSecure = false)
        {
            Name = name;
            Description = description;
            DefaultValue = defaultValue;
            IsSecure = isSecure;
        }
    }
}
