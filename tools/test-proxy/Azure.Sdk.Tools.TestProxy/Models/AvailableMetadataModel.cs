using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.IO;

namespace Azure.Sdk.Tools.TestProxy.Models
{
    public class AvailableMetadataModel : RunTimeMetaDataModel
    {
        public AvailableMetadataModel()
        {
            Descriptions = _populateFromMetadata();
        }

        private List<ActionDescription> _populateFromMetadata()
        {
            XmlDocument docCommentXml = GetDocCommentXML();

            var namespaces = new string[] { "Azure.Sdk.Tools.TestProxy.Sanitizers", "Azure.Sdk.Tools.TestProxy.Matchers", "Azure.Sdk.Tools.TestProxy.Transforms" };
            var extensions = Assembly.GetTypes().Where(t => namespaces.Contains(t.Namespace) && t.Name != "<>c"); //exclude compiler generated types

            var result = extensions.Select(x => new ActionDescription(x.Namespace) {
                Name = x.Name,
                ConstructorDetails = GetCtorDescription(x, docCommentXml),
                Description = GetClassDocComment(x, docCommentXml)
            }).ToList();

            return result;
        }
    }
}
