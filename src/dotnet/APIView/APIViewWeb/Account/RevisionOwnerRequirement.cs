// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Authorization;

namespace APIViewWeb
{
    public class RevisionOwnerRequirement : IAuthorizationRequirement
    {
        protected RevisionOwnerRequirement()
        {
        }

        public static RevisionOwnerRequirement Instance { get; } = new RevisionOwnerRequirement();
    }
}