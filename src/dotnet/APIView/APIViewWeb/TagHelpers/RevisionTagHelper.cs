using Microsoft.AspNetCore.Razor.TagHelpers;

namespace APIViewWeb.TagHelpers
{
    [HtmlTargetElement(Attributes = "author")]
    public class RevisionTagHelper : TagHelper
    {
        public string Author { get; set; }

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            output.Content.SetContent(GetAuthorLabel());
        }

        private string GetAuthorLabel() =>
            !string.IsNullOrEmpty(Author) ? $" by {Author}" : "";
    }
}
