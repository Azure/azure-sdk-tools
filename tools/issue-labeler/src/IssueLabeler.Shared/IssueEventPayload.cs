// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
