// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Markdig;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace APIViewWeb
{
    public static class CommentMarkdownExtensions
    {
        private static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

        public static IHtmlContent FormatAsMarkdown(this IHtmlHelper helper, string text)
        {
            try
            {
                return new HtmlString(Markdown.ToHtml(helper.Encode(text), MarkdownPipeline));
            }
            catch
            {
                return new HtmlString(helper.Encode(text));
            }
        }
    }
}