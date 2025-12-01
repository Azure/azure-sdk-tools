using System.ComponentModel;
using Newtonsoft.Json;

namespace IssueLabeler.Shared
{
    public class DownloaderPayload
    {
        public string OutputPath { get; set; }
        public string RepositoryName { get; set; }
    }
}
