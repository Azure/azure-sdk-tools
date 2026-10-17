// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace APIViewWeb.TagHelpers
{
    [HtmlTargetElement(Attributes = "asp-resource,asp-requirement")]
    public class AuthorizationPolicyTagHelper : TagHelper
    {
        private readonly IAuthorizationService _authorizationService;

        [ViewContext]
        public ViewContext ViewContext { get; set; }

        public AuthorizationPolicyTagHelper(IAuthorizationService authorizationService)
        {
            _authorizationService = authorizationService;
        }

        [HtmlAttributeName("asp-resource")]
        public object Resource { get; set; }

        [HtmlAttributeName("asp-requirement")]
        public IAuthorizationRequirement Requirement { get; set; }

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            var authenticateResult = await _authorizationService.AuthorizeAsync(ViewContext.HttpContext.User, Resource, new [] { Requirement });

            if (!authenticateResult.Succeeded)
            {
                output.SuppressOutput();
            }
        }
    }
}