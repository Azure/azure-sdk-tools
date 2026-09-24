// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Authorization;

namespace APIViewWeb
{
    public class CommentOwnerRequirement : IAuthorizationRequirement
    {
        public static CommentOwnerRequirement Instance { get; } = new CommentOwnerRequirement();

        protected CommentOwnerRequirement()
        {
        }
    }
}