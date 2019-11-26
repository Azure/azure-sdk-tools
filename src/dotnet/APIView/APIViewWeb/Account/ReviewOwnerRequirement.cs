// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Authorization;

namespace APIViewWeb
{
    public class ReviewOwnerRequirement : IAuthorizationRequirement
    {
        protected ReviewOwnerRequirement()
        {
        }

        public static ReviewOwnerRequirement Instance { get; } = new ReviewOwnerRequirement();
    }
}