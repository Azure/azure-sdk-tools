using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace IssueLabeler.Shared
{
    public class IssuePayload
    {
        public int IssueNumber { get; set; }
        public string Title { get; set; }
        public string Body { get; set; }
        public string IssueUserLogin { get; set; }
        public string RepositoryName { get; set; }
        public string RepositoryOwnerName { get; set; }

        [DefaultValue(true)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool PredictLabels { get; set; }

        [DefaultValue(true)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool PredictAnswers { get; set; }
    }
}
