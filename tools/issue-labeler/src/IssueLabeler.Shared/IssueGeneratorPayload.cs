namespace IssueLabeler.Shared
{
    public class IssueGeneratorPayload
    {
        public string RepositoryName { get; set; }
        public string OutputFilename { get; set; }
        public string? CategoryLabels { get; set; }
        public string? ServiceLabels { get; set; }
    }
}
