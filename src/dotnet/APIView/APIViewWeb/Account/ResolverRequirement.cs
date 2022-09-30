// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Authorization;

namespace APIViewWeb
{
    public class ResolverRequirement : IAuthorizationRequirement
    {
        protected ResolverRequirement()
        {
        }

        public static ResolverRequirement Instance { get; } = new ResolverRequirement();
    }
}
