namespace AzureSdkQaBot.Model
{
    public class Citation : IEquatable<Citation>
    {
        public string? Source { get; set; }
        public string? Content { get; set; }

        public Citation(string? source, string? content)
        {
            Source = source;
            Content = content;
        }

        public bool Equals(Citation? other)
        {
            if (other == null)
            {
                return false;
            }
            return string.Equals(Content, other.Content);
        }
    }
}
