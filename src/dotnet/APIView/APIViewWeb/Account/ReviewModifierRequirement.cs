// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Authorization;

namespace APIViewWeb
{
    public class ReviewModifierRequirement : IAuthorizationRequirement
    {
        protected ReviewModifierRequirement()
        {
        }

        public static ReviewModifierRequirement Instance { get; } = new ReviewModifierRequirement();
    }
}