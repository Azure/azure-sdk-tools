using APIView;

namespace ApiView
{
    public readonly struct CodeLine
    {
        public string DisplayString { get; }
        public string ElementId { get; }

        public bool isLineAllDocumentation { get; }
        public CodeLine(string html, string id, bool isLineAllDocumentation)
        {
            this.DisplayString = html;
            this.ElementId = id;
            this.isLineAllDocumentation = isLineAllDocumentation;
        }

        public override string ToString()
        {
            return DisplayString + " [" + ElementId + "]";
        }
    }
}
