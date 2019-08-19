namespace APIView
{
    public struct CodeFileToken
    {

        public CodeFileToken(string value, CodeFileTokenKind kind)
        {
            Value = value;
            NavigateToId = null;
            Kind = kind;
            DefinitionId = null;
        }

        public string DefinitionId { get; set; }
        public string NavigateToId { get; set; }
        public string Value { get; set; }
        public CodeFileTokenKind Kind { get; set; }
    }
}