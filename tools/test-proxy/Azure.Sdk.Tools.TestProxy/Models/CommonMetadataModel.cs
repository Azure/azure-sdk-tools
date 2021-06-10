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

        IEnumerable<ActionDescription> Descriptions;

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
            var allTypes = new List<ActionDescription>();
            XmlDocument docCommentXml = _getDocCommentXML();

            // populate sanitizers, transforms, and matchers using reflection to scan the existing assembly
            // TODO: move to the same iteration loop, with conversion from the namespace -> ActionType
            var sanitizerTypes = _assembly.GetTypes().Where(t => t.Namespace == "Azure.Sdk.Tools.TestProxy.Sanitizers");
            var matcherTypes = _assembly.GetTypes().Where(t => t.Namespace == "Azure.Sdk.Tools.TestProxy.Matchers");
            var transformTypes = _assembly.GetTypes().Where(t => t.Namespace == "Azure.Sdk.Tools.TestProxy.Transforms");

            // convert to action descriptions
            var sanitizers = sanitizerTypes.Select(x => new ActionDescription() {
                ActionType = MetaDataType.Sanitizer,
                Name = x.Name,
                Arguments = _getConstructorFromType(x, docCommentXml),
                Description = _getClassDocComment(x, docCommentXml)
            });
            var matchers = sanitizerTypes.Select(x => new ActionDescription()
            {
                ActionType = MetaDataType.Matcher,
                Name = x.Name,
                Arguments = _getConstructorFromType(x, docCommentXml),
                Description = _getClassDocComment(x, docCommentXml)
            });
            var transforms = sanitizerTypes.Select(x => new ActionDescription()
            {
                ActionType = MetaDataType.Transform,
                Name = x.Name,
                Arguments = _getConstructorFromType(x, docCommentXml),
                Description = _getClassDocComment(x, docCommentXml)
            });

            allTypes.AddRange(sanitizers);
            allTypes.AddRange(matchers);
            allTypes.AddRange(transforms);

            return allTypes;
        }

        private List<Tuple<string, string>> _getConstructorFromType(Type type, XmlDocument docCommentXml)
        {
            List<string> results = new List<string>();
            var ctor = type.GetConstructors()[0];
            var parameters = ctor.GetParameters();

            return parameters.Select(x => new Tuple<string, string>(x.Name, _getCtorDocComment(type, x.Name, docCommentXml))).ToList();
        }

        private string _getCtorDocComment(Type type, string memberName, XmlDocument docCommentXml)
        {
            var memberSearchString = String.Format(CTOR_FORMAT_STRING, type.FullName);

            foreach (XmlElement xmlElement in docCommentXml["doc"]["members"])
            {
                if (xmlElement.Attributes["name"].Value.Contains(memberSearchString))
                {
                    foreach(XmlNode child in xmlElement.ChildNodes)
                    {
                        if(child.Name == "param")
                        {
                            if (child.Attributes["name"].Value.Equals(memberName))
                            {
                                return child.InnerText;
                            }
                        }
                    }
                }
            }

            return String.Empty;
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
