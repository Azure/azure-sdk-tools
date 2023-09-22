namespace SwaggerApiParser.SwaggerApiView
{
    public enum CodeFileTokenKind
    {
        Text = 0,
        Newline = 1,
        Whitespace = 2,
        Punctuation = 3,
        Keyword = 4,
        LineIdMarker = 5,
        TypeName = 6,
        MemberName = 7,
        StringLiteral = 8,
        Literal = 9,
        Comment = 10,
        DocumentRangeStart = 11,
        DocumentRangeEnd = 12,
        DeprecatedRangeStart = 13,
        DeprecatedRangeEnd = 14,
        SkipDiffRangeStart = 15,
        SkipDiffRangeEnd = 16,
        FoldableSectionHeading = 17,
        FoldableSectionContentStart = 18,
        FoldableSectionContentEnd = 19,
        TableBegin = 20,
        TableEnd = 21,
        TableRowCount = 22,
        TableColumnCount = 23,
        TableColumnName = 24,
        TableCellBegin = 25,
        TableCellEnd = 26,
        LeafSectionPlaceholder = 27,
        ExternalLinkStart = 28,
        ExternalLinkEnd = 29,
        HiddenApiRangeStart = 30,
        HiddenApiRangeEnd = 31
    }
    public struct CodeFileToken
    {
        public CodeFileToken(string value, CodeFileTokenKind kind, int? numberOfLinesinLeafSection = null)
        {
            Value = value;
            NavigateToId = null;
            Kind = kind;
            DefinitionId = null;
            CrossLanguageDefId = null;
            NumberOfLinesinLeafSection = numberOfLinesinLeafSection;
        }

        public string DefinitionId { get; set; }

        public string NavigateToId { get; set; }

        public string Value { get; set; }

        public CodeFileTokenKind Kind { get; set; }

        public string CrossLanguageDefId { get; set; }

        public int? NumberOfLinesinLeafSection { get; set; }

        public override string ToString()
        {
            return $"{Value} ({Kind})";
        }
    }
}
