using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.IO;

namespace Azure.Sdk.Tools.TestProxy.Models
{
    public class CommonMetadataModel : PageModel
    {
        private Assembly _assembly = Assembly.GetExecutingAssembly();
        const string CTOR_FORMAT_STRING = "M:{0}.#ctor";
        const string CLASS_FORMAT_STRING = "T:{0}";

        public IEnumerable<ActionDescription> Descriptions;

        public int Length { get { return Descriptions.Count(); } }

        public CommonMetadataModel()
        {
            Descriptions = _populateFromMetadata();
        }

        public IEnumerable<ActionDescription> Transforms
        {
            get { return Descriptions.Where(x => x.ActionType == MetaDataType.Transform); }
        }

        public IEnumerable<ActionDescription> Matchers
        {
            get { return Descriptions.Where(x => x.ActionType == MetaDataType.Matcher); }
        }

        public IEnumerable<ActionDescription> Sanitizers
        {
            get { return Descriptions.Where(x => x.ActionType == MetaDataType.Sanitizer); }
        }

        private List<ActionDescription> _populateFromMetadata()
        {
            XmlDocument docCommentXml = _getDocCommentXML();

            var namespaces = new string[] { "Azure.Sdk.Tools.TestProxy.Sanitizers", "Azure.Sdk.Tools.TestProxy.Matchers", "Azure.Sdk.Tools.TestProxy.Transforms" };
            var extensions = _assembly.GetTypes().Where(t => namespaces.Contains(t.Namespace));

            var result = extensions.Select(x => new ActionDescription(x.Namespace) {
                Name = x.Name,
                ConstructorDetails = _getCtorDescription(x, docCommentXml),
                Description = _getClassDocComment(x, docCommentXml)
            }).ToList();

            return result;
        }

        private CtorDescription _getCtorDescription(Type type, XmlDocument docCommentXml)
        {
            var memberSearchString = String.Format(CTOR_FORMAT_STRING, type.FullName);
            var description = String.Empty;
            var arguments = new List<Tuple<string, string>>();

            foreach (XmlElement xmlElement in docCommentXml["doc"]["members"])
            {
                if (xmlElement.Attributes["name"].Value.Contains(memberSearchString))
                {
                    foreach(XmlNode child in xmlElement.ChildNodes)
                    {
                        if (child.Name == "summary")
                        {
                            description = child.InnerText;
                        }
                        if (child.Name == "param")
                        {
                            arguments.Add(new Tuple<string, string>(child.Attributes["name"].Value, child.InnerText));
                        }
                    }
                }
            }

            return new CtorDescription()
            {
                Description = description,
                Arguments = arguments
            };
        }

        private string _getClassDocComment(Type type, XmlDocument docCommentXml)
        {
            var memberSearchString = String.Format(CLASS_FORMAT_STRING, type.FullName);

            foreach (XmlElement xmlElement in docCommentXml["doc"]["members"])
            {
                if (xmlElement.Attributes["name"].Value.Equals(memberSearchString))
                {
                    return xmlElement.ChildNodes[0].InnerText;
                }
            }

            return String.Empty;
        }

        // for this to work you need to have generatexmldoc activated and the generated comment xml MUST be alongside the assembly
        private XmlDocument _getDocCommentXML()
        {
            var location = _assembly.Location;
            using (var xmlReader = new StreamReader(Path.ChangeExtension(location, ".xml")))
            {
                XmlDocument result = new XmlDocument();
                result.Load(xmlReader);
                return result;
            }
        }

    }
}
