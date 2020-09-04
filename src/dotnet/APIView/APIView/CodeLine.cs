using APIView;

namespace ApiView
{
    public readonly struct CodeLine
    {
        public string DisplayString { get; }
        public string ElementId { get; }
        public bool isDocumentation { get; }

        public CodeLine(string html, string id, bool isDocumentationLine)
        {
            this.DisplayString = html;
            this.ElementId = id;
            this.isDocumentation = isDocumentationLine;
        }

        public override string ToString()
        {
            return DisplayString + " [" + ElementId + "]";
        }
    }
}
