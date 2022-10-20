// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Security.Claims;

namespace APIViewWeb
{
    public class UsageSampleRevisionModel
    {
        public string FileId { get; set; } = IdHelper.GenerateId();
        public string OriginalFileId { get; set; } = IdHelper.GenerateId();
        public string OriginalFileName { get; set; } // likely to be null if uploaded via text
        public string CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; } = DateTime.Now;
        public string RevisionTitle { get; set; }
        public int RevisionNumber { get; set; }
        public bool RevisionIsDeleted { get; set; } = false;
        public UsageSampleRevisionModel(ClaimsPrincipal user, int revisionNumber)
        {
            if(user != null)
            {
                CreatedBy = user.GetGitHubLogin();
            }
            RevisionNumber = revisionNumber;
        }
    }
}
