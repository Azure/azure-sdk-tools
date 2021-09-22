// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
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
        public List<string> Commits { get; set; } = new List<string>();
        public string RepoName { get; set; }
        public string FilePath { get; set; }
        public bool IsOpen { get; set; } = true;
        public string ReviewId { get; set; }
        public string Author { get; set; }
    }
}