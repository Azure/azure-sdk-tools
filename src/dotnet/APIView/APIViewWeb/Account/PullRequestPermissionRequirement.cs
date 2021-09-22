// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Authorization;

namespace APIViewWeb
{
    public class PullRequestPermissionRequirement : IAuthorizationRequirement
    {
        protected PullRequestPermissionRequirement()
        {
        }

        public static PullRequestPermissionRequirement Instance { get; } = new PullRequestPermissionRequirement();
    }
}