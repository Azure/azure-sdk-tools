using System.ComponentModel;
using Newtonsoft.Json;

namespace IssueLabeler.Shared
{
    public class IssueGeneratorPayload
    {
        public string RepositoryName { get; set; }
        public string OutputFilename { get; set; }
        public string? numIssues { get; set; }
        public string? CategoryLabels { get; set; }
        public string? ServiceLabels { get; set; }
        [DefaultValue(true)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool UploadToBlob { get; set; }
    }
}
