// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Authorization;

namespace APIViewWeb
{
    public class AutoAPIRevisionModifierRequirement : IAuthorizationRequirement
    {
        protected AutoAPIRevisionModifierRequirement()
        {
        }

        public static AutoAPIRevisionModifierRequirement Instance { get; } = new AutoAPIRevisionModifierRequirement();
    }
}
