// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Authorization;

namespace APIViewWeb
{
    public class AutoReviewModifierRequirement : IAuthorizationRequirement
    {
        protected AutoReviewModifierRequirement()
        {
        }

        public static AutoReviewModifierRequirement Instance { get; } = new AutoReviewModifierRequirement();
    }
}