using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using APIViewWeb.LeanModels;
using Microsoft.Extensions.Configuration;
using System.Reflection;

namespace APIViewWeb.Services
{
    public interface IEmailTemplateService
    {
        Task<string> GetNamespaceReviewRequestEmailAsync(
            string packageName,
            string typeSpecUrl,
            IEnumerable<ReviewListItemModel> languageReviews,
            string notes = null);

        Task<string> GetNamespaceReviewApprovedEmailAsync(
            string packageName,
            string typeSpecUrl,
            IEnumerable<ReviewListItemModel> languageReviews);
    }

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
            var resourceName = $"APIViewWeb.EmailTemplates.{templateName}";
            
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

            // Generate language links HTML like the pending namespace approval tab
            var languageLinksHtml = GenerateLanguageLinksHtml(languageReviews);

            // Generate notes section HTML if notes are provided
            var notesSectionHtml = string.IsNullOrEmpty(notes) 
                ? "" 
                : $@"<div class=""notes-section"">
                        <div class=""notes-title"">Additional Notes:</div>
                        <div>{notes}</div>
                     </div>";

            // Replace all placeholders
            return template
                .Replace("{{PackageName}}", packageName)
                .Replace("{{TypeSpecUrl}}", typeSpecUrl)
                .Replace("{{LanguageLinks}}", languageLinksHtml)
                .Replace("{{NotesSection}}", notesSectionHtml);
        }

        public async Task<string> GetNamespaceReviewApprovedEmailAsync(
            string packageName,
            string typeSpecUrl,
            IEnumerable<ReviewListItemModel> languageReviews)
        {
            var template = await LoadEmbeddedTemplateAsync("NamespaceReviewApprovedEmail.html");

            // Generate language package names in the simple format
            var languagePackagesHtml = GenerateLanguagePackagesHtml(languageReviews);

            // Replace all placeholders
            return template
                .Replace("{{PackageName}}", packageName)
                .Replace("{{TypeSpecUrl}}", typeSpecUrl)
                .Replace("{{LanguageViews}}", languagePackagesHtml);
        }

        private string GenerateLanguagePackagesHtml(IEnumerable<ReviewListItemModel> languageReviews)
        {
            if (languageReviews == null || !languageReviews.Any())
                return "<li class=\"package-item\">No language-specific package names available yet.</li>";

            var packagesHtml = "";
            foreach (var review in languageReviews)
            {
                // Build format to match the request email styling
                packagesHtml += $@"
                    <li class=""package-item"">
                        <span class=""language-name"">{review.Language}:</span>{review.PackageName}
                    </li>";
            }
            return packagesHtml;
        }

        private string GenerateLanguageLinksHtml(IEnumerable<ReviewListItemModel> languageReviews)
        {
            if (languageReviews == null || !languageReviews.Any())
                return "<li class=\"language-item\">No language-specific reviews available yet.</li>";

            var linksHtml = "";
            foreach (var review in languageReviews)
            {
                linksHtml += $@"
                    <li class=""language-item"">
                        <span class=""language-name"">{review.Language}:</span>
                        <a href=""{_apiviewEndpoint}/Assemblies/Review/{review.Id}"" class=""package-link"">{review.PackageName}</a>
                    </li>";
            }
            return linksHtml;
        }
    }
}
