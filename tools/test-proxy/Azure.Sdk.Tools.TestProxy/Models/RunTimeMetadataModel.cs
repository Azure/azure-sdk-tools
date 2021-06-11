﻿using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.IO;

namespace Azure.Sdk.Tools.TestProxy.Models
{
    public class RunTimeMetaDataModel : PageModel
    {
        public Assembly Assembly = Assembly.GetExecutingAssembly();
        const string CTOR_FORMAT_STRING = "M:{0}.#ctor";
        const string CLASS_FORMAT_STRING = "T:{0}";

        public IEnumerable<ActionDescription> Descriptions;

        public int Length { get { return Descriptions.Count(); } }

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

        public CtorDescription GetCtorDescription(Type type, XmlDocument docCommentXml)
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

        public string GetClassDocComment(Type type, XmlDocument docCommentXml)
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
        public XmlDocument GetDocCommentXML()
        {
            var location = Assembly.Location;
            using (var xmlReader = new StreamReader(Path.ChangeExtension(location, ".xml")))
            {
                XmlDocument result = new XmlDocument();
                result.Load(xmlReader);
                return result;
            }
        }

    }
}
