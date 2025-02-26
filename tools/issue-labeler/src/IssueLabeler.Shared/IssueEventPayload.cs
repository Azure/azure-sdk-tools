// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace IssueLabeler.Shared.Models
{
    public class IssueEventPayload
    {
        public string Action { set; get; }

        public IssueModel Issue { set; get; }

        public PrModel Pull_Request { set; get; }
        public Label Label { set; get; }

        public Repository Repository { get; set; }
    }

    public class Repository
    {
        public string Full_Name { get; set; }

        public string Default_Branch { get; set; }
    }
}
