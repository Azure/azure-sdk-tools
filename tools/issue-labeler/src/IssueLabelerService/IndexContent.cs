// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using IssueLabeler.Shared;

namespace IssueLabelerService
{
    public class IndexContent : IssueTriageContent
    {
        public string Chunk { get; set; }
        public double? Score { get; set; }

        public IndexContent(string id, string repository, string url, string documentType)
        : base(id, repository, url, documentType)
        {
        }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
