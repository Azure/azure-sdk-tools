// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Razor.TagHelpers;

namespace APIViewWeb.TagHelpers
{
    [HtmlTargetElement(Attributes = "active-if")]
    public class ActiveIfTagHelper : TagHelper
    {
        public bool ActiveIf { get; set; }

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            if (ActiveIf)
            {
                output.Attributes.SetAttribute("class", output.Attributes["class"]?.Value + " active");
            }
        }
    }
}