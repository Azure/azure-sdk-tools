using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using System.Linq;
using APIViewWeb.LeanModels;
using APIViewWeb.Helpers;
using Microsoft.Extensions.Configuration;

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
            IEnumerable<ReviewListItemModel> languageReviews,
            bool isAutoApproved = false,
            DateTime? originalRequestDate = null,
            DateTime? autoApprovedDate = null);
    }

    public class EmailTemplateService : IEmailTemplateService
    {
        private readonly IHostEnvironment _hostEnvironment;
        private readonly string _apiviewEndpoint;
        private const int MaxPackageNameLength = 60;
        private const int BusinessDaysForDeadline = 3;

        public EmailTemplateService(IHostEnvironment hostEnvironment, IConfiguration configuration)
        {
            _hostEnvironment = hostEnvironment;
            _apiviewEndpoint = configuration.GetValue<string>("Endpoint");
        }

        public async Task<string> GetNamespaceReviewRequestEmailAsync(
            string packageName,
            string typeSpecUrl,
            IEnumerable<ReviewListItemModel> languageReviews,
            string notes = null)
        {
            var templatePath = Path.Combine(_hostEnvironment.ContentRootPath, "Templates", "NamespaceReviewRequestEmail.html");
            var template = await File.ReadAllTextAsync(templatePath);

            // Calculate deadline (3 business days from now using centralized utility)
            var deadline = DateTimeHelper.CalculateBusinessDays(DateTime.Now, BusinessDaysForDeadline);

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
                .Replace("{{NotesSection}}", notesSectionHtml)
                .Replace("{{ApprovalDeadline}}", deadline.ToString("MMMM dd, yyyy"));
        }

        public async Task<string> GetNamespaceReviewApprovedEmailAsync(
            string packageName,
            string typeSpecUrl,
            IEnumerable<ReviewListItemModel> languageReviews,
            bool isAutoApproved = false,
            DateTime? originalRequestDate = null,
            DateTime? autoApprovedDate = null)
        {
            var templatePath = Path.Combine(_hostEnvironment.ContentRootPath, "Templates", "NamespaceReviewApprovedEmail.html");
            var template = await File.ReadAllTextAsync(templatePath);

            // Generate language package names in the simple format
            var languagePackagesHtml = GenerateLanguagePackagesHtml(languageReviews);

            // Generate auto-approval section if needed
            var autoApprovalSectionHtml = "";
            if (isAutoApproved)
            {
                autoApprovalSectionHtml = $@"
                    <div class=""auto-approval-section"">
                        <div class=""auto-approval-title"">✅ Auto-Approved</div>
                        <div>This namespace review was automatically approved after 3 business days with no comments raised.</div>
                        <div class=""auto-approval-dates"">
                            <strong>Original Request Date:</strong> {originalRequestDate?.ToString("MMMM dd, yyyy") ?? "N/A"}<br/>
                            <strong>Auto-Approved Date:</strong> {autoApprovedDate?.ToString("MMMM dd, yyyy") ?? DateTime.UtcNow.ToString("MMMM dd, yyyy")}
                        </div>
                    </div>";
            }

            // Replace all placeholders
            return template
                .Replace("{{PackageName}}", packageName)
                .Replace("{{TypeSpecUrl}}", typeSpecUrl)
                .Replace("{{LanguageViews}}", languagePackagesHtml)
                .Replace("{{AutoApprovalSection}}", autoApprovalSectionHtml);
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
                var truncatedName = review.PackageName.Length > MaxPackageNameLength 
                    ? review.PackageName.Substring(0, MaxPackageNameLength) + "..." 
                    : review.PackageName;

                // Build HTML that matches the pending namespace approval tab format
                linksHtml += $@"
                    <li class=""language-item"">
                        <span class=""language-name"">{review.Language}:</span>
                        <a href=""{_apiviewEndpoint}/Assemblies/Review/{review.Id}"" class=""package-link"">{truncatedName}</a>
                    </li>";
            }
            return linksHtml;
        }
    }
}
