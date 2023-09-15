// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.TeamFoundation.TestManagement.WebApi;

namespace APIViewWeb.Models
{

    public class AIReviewViolations
    {
        [JsonPropertyName("rule_ids")]
        public List<string> RuleIds { get; set; }
        [JsonPropertyName("line_no")]
        public int LineNo { get; set; }
        [JsonPropertyName("bad_code")]
        public string Code { get; set; }
        [JsonPropertyName("suggestion")]
        public string Suggestion { get; set; }
        [JsonPropertyName("comment")]
        public string Comment { get; set; }
    }

    public class AIReviewModel
    {
        [JsonPropertyName("status")]
        public string Status { get; set; }
        [JsonPropertyName("violations")]
        public List<AIReviewViolations> Violations { get; set; }
    }
}
