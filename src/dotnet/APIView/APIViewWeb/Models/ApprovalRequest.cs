// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel.DataAnnotations;

namespace APIViewWeb.Models
{
    public class ApprovalRequest
    {
        [Required]
        public bool Approve { get; set; }
    }
}
