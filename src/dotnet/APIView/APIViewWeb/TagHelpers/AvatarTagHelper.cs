using Microsoft.AspNetCore.Razor.TagHelpers;

namespace APIViewWeb.TagHelpers
{
    [HtmlTargetElement("img", Attributes = "username, size")]
    public class AvatarTagHelper : TagHelper
    {
        public string Username { get; set; }
        public string Size { get; set; } = "28";

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            string url = "https://github.com/" + Username + ".png?size=" + Size;
            output.Attributes.SetAttribute("src", url);
        }
    }
}
