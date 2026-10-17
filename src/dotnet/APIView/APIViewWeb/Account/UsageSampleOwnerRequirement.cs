// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Authorization;

namespace APIViewWeb
{
    public class UsageSampleOwnerRequirement : IAuthorizationRequirement
    {
        protected UsageSampleOwnerRequirement()
        {
        }

        public static UsageSampleOwnerRequirement Instance { get; } = new UsageSampleOwnerRequirement();
    }
}
