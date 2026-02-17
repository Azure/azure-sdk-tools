using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System.Net;
using APIViewWeb.LeanModels;
using APIViewWeb.Helpers;
using APIViewWeb.Models;
using Microsoft.Extensions.Configuration;
using System.Reflection;

namespace APIViewWeb.Services
{
    public class EmailTemplateService : IEmailTemplateService
    {
        private readonly string _apiviewEndpoint;

        public EmailTemplateService(IConfiguration configuration)
        {
            _apiviewEndpoint = configuration.GetValue<string>("APIVIew-Host-Url");
        }

        private async Task<string> LoadEmbeddedTemplateAsync(string templateName)
        {
            var assembly = typeof(EmailTemplateService).GetTypeInfo().Assembly;
            var resourceName = $"APIViewWeb.EmailTemplates.{templateName.Replace('/', '.').Replace('\\', '.')}";
            
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new FileNotFoundException($"Template {templateName} not found as embedded resource. Resource name: {resourceName}");
                    
                using (var reader = new StreamReader(stream))
                {
                    return await reader.ReadToEndAsync();
                }
            }
        }

        public async Task<string> GetNamespaceReviewRequestEmailAsync(
            string packageName,
            string typeSpecUrl,
            IEnumerable<ReviewListItemModel> languageReviews,
            string notes = null)
        {
            var template = await LoadEmbeddedTemplateAsync("NamespaceReviewRequestEmail.html");

            var languageLinksHtml = await GenerateLanguageLinksHtmlAsync(languageReviews);
            var notesSectionHtml = await GenerateNotesSectionHtmlAsync(notes);

            return RenderTemplate(template, new Dictionary<string, string>
            {
                ["{{PackageName}}"] = Encode(packageName),
                ["{{TypeSpecUrl}}"] = Encode(typeSpecUrl),
                ["{{LanguageLinks}}"] = languageLinksHtml,
                ["{{NotesSection}}"] = notesSectionHtml,
            });
        }

        public async Task<string> GetNamespaceReviewApprovedEmailAsync(
            string packageName,
            string typeSpecUrl,
            IEnumerable<ReviewListItemModel> languageReviews)
        {
            var template = await LoadEmbeddedTemplateAsync("NamespaceReviewApprovedEmail.html");

            var languagePackagesHtml = await GenerateLanguagePackagesHtmlAsync(languageReviews);

            return RenderTemplate(template, new Dictionary<string, string>
            {
                ["{{PackageName}}"] = Encode(packageName),
                ["{{TypeSpecUrl}}"] = Encode(typeSpecUrl),
                ["{{LanguagePackages}}"] = languagePackagesHtml,
            });
        }

        public async Task<string> GetApproverReviewEmailAsync(string requesterUserName, string reviewId, string reviewName)
        {
            var template = await LoadEmbeddedTemplateAsync("ApproverReviewRequestEmail.html");
            return RenderTemplate(template, new Dictionary<string, string>
            {
                ["{{RequesterProfileUrl}}"] = Encode($"{_apiviewEndpoint}/Assemblies/Profile/{requesterUserName}"),
                ["{{RequesterUserName}}"] = Encode(requesterUserName),
                ["{{ReviewUrl}}"] = Encode($"{_apiviewEndpoint}/Assemblies/Review/{reviewId}"),
                ["{{ReviewName}}"] = Encode(reviewName),
                ["{{RequestedReviewsUrl}}"] = Encode($"{_apiviewEndpoint}/Assemblies/RequestedReviews/"),
            });
        }

        public async Task<string> GetCommentTagEmailAsync(CommentItemModel comment, ReviewListItemModel review, string reviewUrl)
        {
            var template = await LoadEmbeddedTemplateAsync("CommentTagEmail.html");
            return RenderTemplate(template, new Dictionary<string, string>
            {
                ["{{PosterProfileUrl}}"] = Encode($"{_apiviewEndpoint}/Assemblies/Profile/{comment.CreatedBy}"),
                ["{{PosterUserName}}"] = Encode(comment.CreatedBy),
                ["{{ReviewUrl}}"] = Encode(reviewUrl),
                ["{{ReviewName}}"] = Encode(review.PackageName),
                ["{{CommentBodyHtml}}"] = CommentMarkdownExtensions.MarkdownAsHtml(comment.CommentText),
            });
        }

        public async Task<string> GetSubscriberCommentEmailAsync(CommentItemModel comment, string elementUrl = null)
        {
            var template = await LoadEmbeddedTemplateAsync("SubscriberCommentEmail.html");
            var elementSection = string.IsNullOrEmpty(comment.ElementId) || string.IsNullOrEmpty(elementUrl)
                ? string.Empty
                : $"In <a href='{Encode(elementUrl)}'>{Encode(comment.ElementId)}</a>:<br><br>";

            return RenderTemplate(template, new Dictionary<string, string>
            {
                ["{{Heading}}"] = GetContentHeading(comment, includeHtml: true),
                ["{{ElementSection}}"] = elementSection,
                ["{{CommentBodyHtml}}"] = CommentMarkdownExtensions.MarkdownAsHtml(comment.CommentText),
            });
        }

        public async Task<string> GetNewRevisionEmailAsync(ReviewListItemModel review, APIRevisionListItemModel revision)
        {
            var template = await LoadEmbeddedTemplateAsync("NewRevisionEmail.html");
            return RenderTemplate(template, new Dictionary<string, string>
            {
                ["{{RevisionUrl}}"] = Encode($"{_apiviewEndpoint}/Assemblies/Review/{review.Id}"),
                ["{{RevisionLabel}}"] = Encode(PageModelHelpers.ResolveRevisionLabel(revision)),
                ["{{CreatedBy}}"] = Encode(revision.CreatedBy),
            });
        }

        private async Task<string> GenerateLanguagePackagesHtmlAsync(IEnumerable<ReviewListItemModel> languageReviews)
        {
            if (languageReviews == null || !languageReviews.Any())
                return "<li class=\"package-item\">No language-specific package names available yet.</li>";

            var itemTemplate = await LoadEmbeddedTemplateAsync("Partials/LanguagePackageItem.html");

            return string.Join(string.Empty, languageReviews.Select(review => RenderTemplate(itemTemplate, new Dictionary<string, string>
            {
                ["{{LanguageName}}"] = Encode(review.Language),
                ["{{PackageName}}"] = Encode(review.PackageName),
            })));
        }

        private async Task<string> GenerateLanguageLinksHtmlAsync(IEnumerable<ReviewListItemModel> languageReviews)
        {
            if (languageReviews == null || !languageReviews.Any())
                return "<li class=\"language-item\">No language-specific reviews available yet.</li>";

            var itemTemplate = await LoadEmbeddedTemplateAsync("Partials/LanguageLinkItem.html");

            return string.Join(string.Empty, languageReviews.Select(review => RenderTemplate(itemTemplate, new Dictionary<string, string>
            {
                ["{{LanguageName}}"] = Encode(review.Language),
                ["{{ReviewUrl}}"] = Encode($"{_apiviewEndpoint}/Assemblies/Review/{review.Id}"),
                ["{{PackageName}}"] = Encode(review.PackageName),
            })));
        }

        private async Task<string> GenerateNotesSectionHtmlAsync(string notes)
        {
            if (string.IsNullOrWhiteSpace(notes))
                return string.Empty;

            var notesTemplate = await LoadEmbeddedTemplateAsync("Partials/NotesSection.html");
            return RenderTemplate(notesTemplate, new Dictionary<string, string>
            {
                ["{{Notes}}"] = Encode(notes),
            });
        }

        private static string RenderTemplate(string template, IDictionary<string, string> tokens)
        {
            var rendered = template;
            foreach (var token in tokens)
            {
                rendered = rendered.Replace(token.Key, token.Value ?? string.Empty);
            }

            return rendered;
        }

        private static string Encode(string value)
        {
            return WebUtility.HtmlEncode(value ?? string.Empty);
        }

        private static string GetContentHeading(CommentItemModel comment, bool includeHtml)
        {
            return $"{(includeHtml ? $"<b>{Encode(comment.CreatedBy)}</b>" : $"{Encode(comment.CreatedBy)}")} commented on this review.";
        }
    }
}
