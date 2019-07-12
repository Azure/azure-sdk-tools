using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using System;
using System.Collections.Generic;
using System.Text;

namespace PipelineGenerator
{
    public static class BuildDefinitionExtensions
    {
        public static string GetWebUrl(this BuildDefinition definition)
        {
            var referenceLink = (ReferenceLink)definition.Links.Links["web"];
            return referenceLink.Href;
        }

        public static string GetWebUrl(this BuildDefinitionReference definitionReference)
        {
            var referenceLink = (ReferenceLink)definitionReference.Links.Links["web"];
            return referenceLink.Href;
        }
    }
}
