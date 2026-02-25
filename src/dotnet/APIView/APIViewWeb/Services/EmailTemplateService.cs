using System;
using System.IO;
using System.Threading.Tasks;
using APIViewWeb.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace APIViewWeb.Services
{
    public class EmailTemplateService : IEmailTemplateService
    {
        private const string EmailLogoUrl = "https://raw.githubusercontent.com/Azure/azure-sdk-tools/main/src/dotnet/APIView/APIViewWeb/wwwroot/icons/apiview.png";
        private readonly IServiceProvider _serviceProvider;

        public EmailTemplateService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task<string> RenderAsync<TModel>(EmailTemplateKey templateKey, TModel model)
        {
            var templateName = ResolveTemplateName(templateKey);

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
            viewData["EmailLogoUrl"] = EmailLogoUrl;

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

        private static string ResolveTemplateName(EmailTemplateKey templateKey)
        {
            return templateKey switch
            {
                EmailTemplateKey.NamespaceReviewRequest => "NamespaceReviewRequestEmail.cshtml",
                EmailTemplateKey.NamespaceReviewApproved => "NamespaceReviewApprovedEmail.cshtml",
                EmailTemplateKey.ReviewerAssigned => "ReviewerAssignedEmail.cshtml",
                EmailTemplateKey.CommentTag => "CommentTagEmail.cshtml",
                EmailTemplateKey.SubscriberComment => "SubscriberCommentEmail.cshtml",
                EmailTemplateKey.NewRevision => "NewRevisionEmail.cshtml",
                _ => throw new ArgumentOutOfRangeException(nameof(templateKey), templateKey, "Unsupported email template key"),
            };
        }
    }
}
