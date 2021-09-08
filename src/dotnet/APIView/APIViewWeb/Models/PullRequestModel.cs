using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace APIViewWeb.Models
{
    public class PullRequestModel
    {
        [JsonProperty("id")]
        public string PullRequestId { get; set; } = IdHelper.GenerateId();
        public int PullRequestNumber { get; set; }
        public string CommitSha { get; set; }
        public string Language { get; set; }
    }
}