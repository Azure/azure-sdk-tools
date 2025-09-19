// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using APIViewWeb.LeanModels;

namespace APIViewWeb.Models
{

    public class AIReviewComment
    {
        private List<string> _guidelineIds;

        [JsonPropertyName("rule_ids")]
        public List<string> RuleIds 
        { 
            get => _guidelineIds; 
            set => _guidelineIds = value; 
        }
        
        [JsonPropertyName("guideline_ids")]
        public List<string> GuidelineIds 
        { 
            get => _guidelineIds; 
            set => _guidelineIds = value; 
        }
        [JsonPropertyName("memory_ids")]
        public List<string> MemoryIds { get; set; }
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
        [JsonPropertyName("is_generic")]
        public bool IsGeneric { get; set; }
    }

    public class AIReviewJobPolledResponseModel
    {
        [JsonPropertyName("status")]
        public string Status { get; set; }
        [JsonPropertyName("comments")]
        public List<AIReviewComment> Comments { get; set; }
        [JsonPropertyName("details")]
        public string Details { get; set; }
    }

    public class AIReviewJobStartedResponseModel
    {
        [JsonPropertyName("job_id")]
        public string JobId { get; set; }
    }

    public class AIReviewJobCompletedModel
    {
        [JsonPropertyName("reviewId")]
        public string ReviewId { get; set; }
        [JsonPropertyName("apirevisionId")]
        public string APIRevisionId { get; set; }
        [JsonPropertyName("status")]
        public string Status { get; set; }
        [JsonPropertyName("details")]
        public string Details { get; set; }
        [JsonPropertyName("createdBy")]
        public string CreatedBy { get; set; }
        [JsonPropertyName("jobId")]
        public string JobId { get; set; }
        [JsonPropertyName("noOfGeneratedComments")]
        public int NoOfGeneratedComment { get; set; }
    }

    public class AIReviewJobInfoModel
    {
        public string JobId { get; set; }
        public APIRevisionListItemModel APIRevision { get; set; }
        public List<(string lineText, string lineId)> CodeLines { get; set; }
        public string CreatedBy { get; set; }
    }
}
