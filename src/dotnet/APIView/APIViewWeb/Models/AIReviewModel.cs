// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace APIViewWeb.Models
{

    public class AIReviewComment
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
        [JsonPropertyName("source")]
        public string Source { get; set; }
    }

    public class AIReviewModel
    {
        [JsonPropertyName("comments")]
        public List<AIReviewComment> Comments { get; set; }
    }

    public class CommentModelForCopilot
    {
        [JsonPropertyName("line_no")]
        public int LineNumber { get; set; }
        [JsonPropertyName("author")]
        public string Author { get; set; }
        [JsonPropertyName("comment")]
        public string CommentText { get; set; }
    }
}
