// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
namespace APIViewWeb.Models
{
    public class APIRevisionGenerationPipelineParamModel
    {
        public string ReviewID { get; set; }
        public string RevisionID { get; set; }
        public string FileID { get; set; }
        public string FileName { get; set; }
        public string SourceRepoName { get; set; }
        public string SourceBranchName { get; set; }
    }
}
