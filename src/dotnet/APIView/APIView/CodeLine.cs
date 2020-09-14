using APIView;

namespace ApiView
{
    public readonly struct CodeLine
    {
        public string DisplayString { get; }
        public string ElementId { get; }
        public bool IsDocumentationLine { get; }

        public CodeLine(string html, string id, bool isDocumentation)
        {
            this.DisplayString = html;
            this.ElementId = id;
            this.IsDocumentationLine = isDocumentation;
        }

        public override string ToString()
        {
            return DisplayString + " [" + ElementId + "]";
        }
    }
}
