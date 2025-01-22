namespace AzureSdkQaBot.Model
{
    public class DocumentCitation : IEquatable<DocumentCitation>
    {
        public string? Source { get; set; }
        public string? Content { get; set; }

        public DocumentCitation(string? source, string? content)
        {
            Source = source;
            Content = content;
        }

        public bool Equals(DocumentCitation? other)
        {
            if (other == null)
            {
                return false;
            }
            return string.Equals(Content, other.Content);
        }
    }
}
