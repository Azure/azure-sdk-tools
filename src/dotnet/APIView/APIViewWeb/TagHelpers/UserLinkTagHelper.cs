using Microsoft.AspNetCore.Razor.TagHelpers;

namespace APIViewWeb.TagHelpers
{
    [HtmlTargetElement("a", Attributes = "username")]
    public class UserLinkTagHelper : TagHelper
    {
        public string Username { get; set; }

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            string url = "/Assemblies/Profile/" + Username;
            output.Attributes.SetAttribute("href", url);
        }
    }
}
