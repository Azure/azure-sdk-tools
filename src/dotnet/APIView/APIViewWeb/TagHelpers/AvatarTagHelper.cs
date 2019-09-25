using Microsoft.AspNetCore.Razor.TagHelpers;

namespace APIViewWeb.TagHelpers
{
    [HtmlTargetElement("img", Attributes = "username")]
    public class AvatarTagHelper : TagHelper
    {
        public string Username { get; set; }

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            string url = "https://github.com/" +  Username + ".png";
            output.Attributes.SetAttribute("src", url);
            output.Attributes.SetAttribute("height", "28");
            output.Attributes.SetAttribute("width", "28");
        }
    }
}
