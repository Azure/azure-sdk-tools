using Microsoft.AspNetCore.Razor.TagHelpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace APIViewWeb.TagHelpers
{
    [HtmlTargetElement(Attributes = "avatar")]
    public class AvatarTagHelper : TagHelper
    {
        public string Username { get; set; }

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            string url = "https://github.com/" +  Username + ".png";
            output.Attributes.SetAttribute("src", url);
        }
    }
}
