// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Sdk.Tools.TestProxy.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using System.IO;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.TestProxy
{
    [ApiController]
    [Route("[controller]/[action]")]
    public sealed class Info : Controller
    {
        private readonly RecordingHandler _recordingHandler;

        public Info(RecordingHandler recordingHandler) => _recordingHandler = recordingHandler;

        [HttpGet]
        public async Task<ContentResult> Available()
        {
            var dataModel = new AvailableMetadataModel();
            var viewHtml = await RenderViewAsync(this, "MetadataDump", dataModel);

            return new ContentResult
            {
                ContentType = "text/html",
                Content = viewHtml
            };
        }

        [HttpGet]
        public async Task<ContentResult> Active()
        {
            var dataModel = new ActiveMetadataModel(_recordingHandler);
            var viewHtml = await RenderViewAsync(this, "ActiveExtensions", dataModel);

            return new ContentResult
            {
                ContentType = "text/html",
                Content = viewHtml
            };
        }

        public async Task<string> RenderViewAsync<TModel>(Controller controller, string viewName, TModel model, bool partial = false)
        {
            if (string.IsNullOrEmpty(viewName))
            {
                viewName = controller.ControllerContext.ActionDescriptor.ActionName;
            }

            controller.ViewData.Model = model;

            using (var writer = new StringWriter())
            {
                

                IViewEngine viewEngine = controller.HttpContext.RequestServices.GetService(typeof(ICompositeViewEngine)) as ICompositeViewEngine;
                
                ViewEngineResult viewResult = viewEngine.FindView(controller.ControllerContext, viewName, !partial);

                if (viewResult.Success == false)
                {
                    return $"A view with the name {viewName} could not be found";
                }

                ViewContext viewContext = new ViewContext(
                    controller.ControllerContext,
                    viewResult.View,
                    controller.ViewData,
                    controller.TempData,
                    writer,
                    new HtmlHelperOptions()
                );

                await viewResult.View.RenderAsync(viewContext);

                return writer.GetStringBuilder().ToString();
            }
        }
    }
}
