// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Authorization;

namespace APIViewWeb
{
    public class ApproverRequirement : IAuthorizationRequirement
    {
        protected ApproverRequirement()
        {
        }

        public static ApproverRequirement Instance { get; } = new ApproverRequirement();
    }
}