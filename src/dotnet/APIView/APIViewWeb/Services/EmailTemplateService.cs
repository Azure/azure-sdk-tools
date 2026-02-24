using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using APIViewWeb.LeanModels;
using APIViewWeb.Helpers;
using APIViewWeb.Models;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace APIViewWeb.Services
{
    public class EmailTemplateService : IEmailTemplateService
    {
        private readonly string _apiviewEndpoint;
        private readonly IServiceProvider _serviceProvider;

        public EmailTemplateService(IConfiguration configuration, IServiceProvider serviceProvider)
        {
            _apiviewEndpoint = configuration.GetValue<string>("APIVIew-Host-Url");
            _serviceProvider = serviceProvider;
        }

        public async Task<string> GetNamespaceReviewRequestEmailAsync(
            string packageName,
            string typeSpecUrl,
            IEnumerable<ReviewListItemModel> languageReviews,
            string notes = null)
        {
            return await RenderTemplateAsync("NamespaceReviewRequestEmail.cshtml", new NamespaceReviewRequestEmailModel
            {
                PackageName = packageName,
                TypeSpecUrl = typeSpecUrl,
                LanguageReviews = BuildLanguageReviewModels(languageReviews),
                Notes = notes ?? string.Empty,
            });
        }

        public async Task<string> GetNamespaceReviewApprovedEmailAsync(
            string packageName,
            string typeSpecUrl,
            IEnumerable<ReviewListItemModel> languageReviews)
        {
            return await RenderTemplateAsync("NamespaceReviewApprovedEmail.cshtml", new NamespaceReviewApprovedEmailModel
            {
                PackageName = packageName,
                TypeSpecUrl = typeSpecUrl,
                LanguageReviews = BuildLanguageReviewModels(languageReviews),
            });
        }

        public async Task<string> GetReviewerAssignedEmailAsync(string requesterUserName, string reviewId, string reviewName)
        {
            return await RenderTemplateAsync("ReviewerAssignedEmail.cshtml", new ReviewerAssignedEmailModel
            {
                RequesterProfileUrl = $"{_apiviewEndpoint}/Assemblies/Profile/{requesterUserName}",
                RequesterUserName = requesterUserName,
                ReviewUrl = $"{_apiviewEndpoint}/Assemblies/Review/{reviewId}",
                ReviewName = reviewName,
                RequestedReviewsUrl = $"{_apiviewEndpoint}/Assemblies/RequestedReviews/",
            });
        }

        public async Task<string> GetCommentTagEmailAsync(CommentItemModel comment, ReviewListItemModel review, string reviewUrl)
        {
            return await RenderTemplateAsync("CommentTagEmail.cshtml", new CommentTagEmailModel
            {
                PosterProfileUrl = $"{_apiviewEndpoint}/Assemblies/Profile/{comment.CreatedBy}",
                PosterUserName = comment.CreatedBy,
                ReviewUrl = reviewUrl,
                ReviewName = review.PackageName,
                CommentBodyHtml = new HtmlString(CommentMarkdownExtensions.MarkdownAsHtml(comment.CommentText)),
            });
        }

        public async Task<string> GetSubscriberCommentEmailAsync(CommentItemModel comment, string elementUrl = null)
        {
            return await RenderTemplateAsync("SubscriberCommentEmail.cshtml", new SubscriberCommentEmailModel
            {
                CommentedBy = comment.CreatedBy,
                ElementUrl = elementUrl ?? string.Empty,
                ElementId = comment.ElementId ?? string.Empty,
                HasElementLink = !string.IsNullOrEmpty(comment.ElementId) && !string.IsNullOrEmpty(elementUrl),
                CommentBodyHtml = new HtmlString(CommentMarkdownExtensions.MarkdownAsHtml(comment.CommentText)),
            });
        }

        public async Task<string> GetNewRevisionEmailAsync(ReviewListItemModel review, APIRevisionListItemModel revision)
        {
            return await RenderTemplateAsync("NewRevisionEmail.cshtml", new NewRevisionEmailModel
            {
                RevisionUrl = $"{_apiviewEndpoint}/Assemblies/Review/{review.Id}",
                RevisionLabel = PageModelHelpers.ResolveRevisionLabel(revision),
                CreatedBy = revision.CreatedBy,
            });
        }

        private List<EmailLanguageReviewModel> BuildLanguageReviewModels(IEnumerable<ReviewListItemModel> languageReviews)
        {
            return languageReviews?.Select(review => new EmailLanguageReviewModel
            {
                LanguageName = review.Language,
                PackageName = review.PackageName,
                ReviewUrl = $"{_apiviewEndpoint}/Assemblies/Review/{review.Id}",
            }).ToList() ?? [];
        }

        private async Task<string> RenderTemplateAsync<TModel>(string templateName, TModel model)
        {
            using var scope = _serviceProvider.CreateScope();
            var scopedServices = scope.ServiceProvider;

            var viewEngine = scopedServices.GetRequiredService<IRazorViewEngine>();
            var tempDataProvider = scopedServices.GetRequiredService<ITempDataProvider>();

            var actionContext = new ActionContext(
                new DefaultHttpContext { RequestServices = scopedServices },
                new RouteData(),
                new ActionDescriptor());

            var viewPath = $"/EmailTemplates/{templateName}";
            var viewResult = viewEngine.GetView(executingFilePath: null, viewPath: viewPath, isMainPage: true);

            if (!viewResult.Success)
            {
                throw new FileNotFoundException($"Template '{templateName}' was not found. Path attempted: {viewPath}");
            }

            await using var output = new StringWriter();
            var viewData = new ViewDataDictionary<TModel>(
                metadataProvider: new EmptyModelMetadataProvider(),
                modelState: new ModelStateDictionary())
            {
                Model = model,
            };

            var viewContext = new ViewContext(
                actionContext,
                viewResult.View,
                viewData,
                new TempDataDictionary(actionContext.HttpContext, tempDataProvider),
                output,
                new HtmlHelperOptions());

            await viewResult.View.RenderAsync(viewContext);
            return output.ToString();
        }
    }
}
