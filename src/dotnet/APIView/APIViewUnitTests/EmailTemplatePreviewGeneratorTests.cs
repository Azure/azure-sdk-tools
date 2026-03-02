using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using APIViewWeb;
using APIViewWeb.LeanModels;
using APIViewWeb.Models;
using APIViewWeb.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace APIViewUnitTests
{
    // This test renders all APIView email templates and writes preview HTML files to
    // artifacts/bin/APIViewUnitTests/email-previews for local visual inspection.
    public class EmailTemplatePreviewGeneratorTests
    {
        [Fact(Skip = "Local visual inspection test. Enable explicitly for local runs when preview files are needed.")]
        public async Task GenerateEmailPreviews()
        {
            string repoRoot = FindRepositoryRoot();
            string apiViewWebProjectPath = Path.Combine(repoRoot, "src", "dotnet", "APIView", "APIViewWeb");
            string outputPath = Path.Combine(repoRoot, "artifacts", "bin", "APIViewUnitTests", "email-previews");

            Directory.CreateDirectory(outputPath);

            ServiceProvider services = BuildServiceProvider(apiViewWebProjectPath);
            var renderer = services.GetRequiredService<IEmailTemplateService>();

            const string endpoint = "https://apiview.contoso.local";
            var review = new ReviewListItemModel
            {
                Id = "review-sample-001",
                PackageName = "Azure.Sample.Service",
                Language = "TypeSpec",
                CreatedBy = "jane-approver",
                CreatedOn = DateTime.UtcNow,
            };

            var languageReviews = new List<ReviewListItemModel>
            {
                new ReviewListItemModel { Id = "review-csharp-001", Language = "C#", PackageName = "Azure.Sample.Service" },
                new ReviewListItemModel { Id = "review-python-001", Language = "Python", PackageName = "azure-sample-service" },
                new ReviewListItemModel { Id = "review-java-001", Language = "Java", PackageName = "com.azure.sample.service" },
            };

            var revision = new APIRevisionListItemModel
            {
                Id = "revision-sample-001",
                ReviewId = review.Id,
                PackageName = review.PackageName,
                APIRevisionType = APIRevisionType.Manual,
                Label = "v1 preview",
                CreatedBy = "john-author",
                CreatedOn = DateTime.UtcNow,
            };

            var comment = new CommentItemModel
            {
                ReviewId = review.Id,
                APIRevisionId = revision.Id,
                ElementId = "SampleClient.CreateAsync",
                CommentText = "Looks good overall. Consider renaming the method to make side effects clearer.",
                CreatedBy = "reviewer01",
            };

            string reviewUrl = $"{endpoint}/Assemblies/Review/{review.Id}";

            var templates = new (EmailTemplateKey Key, object Model, string OutputName)[]
            {
                (
                    EmailTemplateKey.NamespaceReviewRequest,
                    NamespaceReviewRequestEmailModel.Create(
                        review.PackageName,
                        reviewUrl,
                        languageReviews,
                        "Please focus on naming consistency across languages.",
                        endpoint),
                    "namespace-review-request.html"
                ),
                (
                    EmailTemplateKey.NamespaceReviewApproved,
                    NamespaceReviewApprovedEmailModel.Create(
                        review.PackageName,
                        reviewUrl,
                        languageReviews,
                        endpoint),
                    "namespace-review-approved.html"
                ),
                (
                    EmailTemplateKey.ReviewerAssigned,
                    ReviewerAssignedEmailModel.Create(endpoint, "jane-approver", review.Id, review.PackageName),
                    "reviewer-assigned.html"
                ),
                (
                    EmailTemplateKey.CommentTag,
                    CommentTagEmailModel.Create(endpoint, comment, review, reviewUrl),
                    "comment-tag.html"
                ),
                (
                    EmailTemplateKey.SubscriberComment,
                    SubscriberCommentEmailModel.Create(endpoint, comment, $"{reviewUrl}#SampleClient.CreateAsync"),
                    "subscriber-comment.html"
                ),
                (
                    EmailTemplateKey.NewRevision,
                    NewRevisionEmailModel.Create(endpoint, review, revision),
                    "new-revision.html"
                ),
            };

            foreach (var template in templates)
            {
                string html = await RenderAsync(renderer, template.Key, template.Model);
                string outputFile = Path.Combine(outputPath, template.OutputName);
                await File.WriteAllTextAsync(outputFile, html, Encoding.UTF8);
            }

            await File.WriteAllTextAsync(
                Path.Combine(outputPath, "index.html"),
                BuildIndexHtml(templates.Select(template => template.OutputName)));

            Assert.All(templates, template => Assert.True(File.Exists(Path.Combine(outputPath, template.OutputName))));
        }

        private static ServiceProvider BuildServiceProvider(string apiViewWebProjectPath)
        {
            var services = new ServiceCollection();
            var environment = new PreviewWebHostEnvironment(apiViewWebProjectPath);

            services.AddSingleton<IWebHostEnvironment>(environment);
            services.AddSingleton<IHostEnvironment>(environment);
            services.AddSingleton<DiagnosticSource>(_ => new DiagnosticListener("APIView.EmailTemplatePreview"));
            services.AddSingleton<DiagnosticListener>(provider => (DiagnosticListener)provider.GetRequiredService<DiagnosticSource>());

#pragma warning disable ASP5001 // Type or member is obsolete
#pragma warning disable CS0618 // Type or member is obsolete
            services.AddMvc()
                .SetCompatibilityVersion(CompatibilityVersion.Latest)
                .AddApplicationPart(typeof(Startup).Assembly)
                .AddRazorRuntimeCompilation(options =>
                {
                    options.FileProviders.Add(new PhysicalFileProvider(apiViewWebProjectPath));
                });
#pragma warning restore CS0618 // Type or member is obsolete
#pragma warning restore ASP5001 // Type or member is obsolete

            services.AddSingleton<IEmailTemplateService, EmailTemplateService>();
            return services.BuildServiceProvider();
        }

        private static async Task<string> RenderAsync(IEmailTemplateService renderer, EmailTemplateKey key, object model)
        {
            return key switch
            {
                EmailTemplateKey.NamespaceReviewRequest => await renderer.RenderAsync(key, (NamespaceReviewRequestEmailModel)model),
                EmailTemplateKey.NamespaceReviewApproved => await renderer.RenderAsync(key, (NamespaceReviewApprovedEmailModel)model),
                EmailTemplateKey.ReviewerAssigned => await renderer.RenderAsync(key, (ReviewerAssignedEmailModel)model),
                EmailTemplateKey.CommentTag => await renderer.RenderAsync(key, (CommentTagEmailModel)model),
                EmailTemplateKey.SubscriberComment => await renderer.RenderAsync(key, (SubscriberCommentEmailModel)model),
                EmailTemplateKey.NewRevision => await renderer.RenderAsync(key, (NewRevisionEmailModel)model),
                _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unsupported template key"),
            };
        }

        private static string BuildIndexHtml(IEnumerable<string> fileNames)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html><head><meta charset=\"utf-8\" /><title>Email Previews</title></head><body>");
            sb.AppendLine("<h2>Email Template Previews</h2><ul>");
            foreach (string fileName in fileNames)
            {
                sb.AppendLine($"<li><a href=\"{fileName}\">{fileName}</a></li>");
            }
            sb.AppendLine("</ul></body></html>");
            return sb.ToString();
        }

        private static string FindRepositoryRoot()
        {
            DirectoryInfo directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory != null)
            {
                bool hasSrc = Directory.Exists(Path.Combine(directory.FullName, "src"));
                bool hasArtifacts = Directory.Exists(Path.Combine(directory.FullName, "artifacts"));
                bool hasReadme = File.Exists(Path.Combine(directory.FullName, "README.md"));

                if (hasSrc && hasArtifacts && hasReadme)
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate repository root for email preview output.");
        }

        private sealed class PreviewWebHostEnvironment : IWebHostEnvironment, IHostEnvironment
        {
            public PreviewWebHostEnvironment(string contentRootPath)
            {
                ApplicationName = typeof(Startup).Assembly.GetName().Name;
                EnvironmentName = Environments.Development;
                ContentRootPath = contentRootPath;
                ContentRootFileProvider = new PhysicalFileProvider(contentRootPath);

                WebRootPath = Path.Combine(contentRootPath, "wwwroot");
                WebRootFileProvider = Directory.Exists(WebRootPath)
                    ? new PhysicalFileProvider(WebRootPath)
                    : new NullFileProvider();
            }

            public string ApplicationName { get; set; }

            public IFileProvider ContentRootFileProvider { get; set; }

            public string ContentRootPath { get; set; }

            public string EnvironmentName { get; set; }

            public string WebRootPath { get; set; }

            public IFileProvider WebRootFileProvider { get; set; }
        }
    }
}
