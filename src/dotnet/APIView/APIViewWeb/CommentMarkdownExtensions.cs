// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Markdig;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace APIViewWeb
{
    public static class CommentMarkdownExtensions
    {
        private static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .DisableHtml()
            .Build();

        public static IHtmlContent FormatAsMarkdown(this IHtmlHelper helper, string text)
        {
            try
            {
                string htmlContent = MarkdownAsHtml(text);
                htmlContent = AddTargetBlankToLinks(htmlContent);
                return new HtmlString(htmlContent);
            }
            catch
            {
                return new HtmlString(helper.Encode(text));
            }
        }

        public static string MarkdownAsHtml(string text) =>
            Markdown.ToHtml(text ?? "", MarkdownPipeline);

        public static string MarkdownAsPlainText(string text) =>
            Markdown.ToPlainText(text ?? "", MarkdownPipeline);

        private static string AddTargetBlankToLinks(string htmlContent)
        {
            return System.Text.RegularExpressions.Regex.Replace(htmlContent, "<a(?!.*?target=)(.*?)>", "<a$1 target=\"_blank\" rel=\"noopener noreferrer\">");
        }
    }
}
