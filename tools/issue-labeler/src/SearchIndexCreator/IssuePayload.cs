// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace SearchIndexCreator
{
    public class IssuePayload
        {
            public int IssueNumber { get; set; }
            public string Title { get; set; }
            public string Body { get; set; }
            public string IssueUserLogin { get; set; }
            public string RepositoryName { get; set; }
            public string RepositoryOwnerName { get; set; }
        }
}