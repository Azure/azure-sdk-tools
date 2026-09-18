using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Xml;
using System.Xml.XPath;
using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Common.Exceptions;

namespace Azure.Sdk.Tools.TestProxy.Sanitizers
{
    /// <summary>
    /// Replaces XML leaf element, attribute, or text values selected by an XPath expression.
    /// Only applies to XML content types. DTDs and malformed XML are rejected.
    /// </summary>
    public class BodyXmlSanitizer : RecordedTestSanitizer
    {
        private readonly string _newValue;
        private readonly XPathExpression _expression;
        private readonly List<BodyXmlSanitizer> _batchedSanitizers;

        /// <summary>
        /// Creates an XML body sanitizer using XPath 1.0. Use local-name() to select elements regardless of namespace.
        /// </summary>
        /// <param name="xmlPath">An XPath selecting leaf elements, attributes, or text nodes.</param>
        /// <param name="value">The replacement text. XML special characters are escaped automatically.</param>
        /// <param name="condition">An optional condition restricting which request/response pairs are sanitized.</param>
        public BodyXmlSanitizer(string xmlPath, string value = "Sanitized", ApplyCondition condition = null)
        {
            _scope = SanitizerScope.Body;
            _newValue = value ?? string.Empty;
            Condition = condition;
            try
            {
                _expression = XPathExpression.Compile(xmlPath);
                _expression.SetContext(new XmlNamespaceManager(new NameTable()));
                if (_expression.ReturnType != XPathResultType.NodeSet)
                {
                    throw new XPathException("The expression must select XML nodes.");
                }
            }
            catch (Exception exception) when (exception is XPathException || exception is ArgumentException)
            {
                throw new HttpException(HttpStatusCode.BadRequest, $"Invalid XML path '{xmlPath}': {exception.Message}");
            }
        }

        private BodyXmlSanitizer(List<BodyXmlSanitizer> sanitizers)
        {
            _scope = SanitizerScope.Body;
            _batchedSanitizers = sanitizers;
        }

        internal static IEnumerable<RecordedTestSanitizer> Batch(IEnumerable<RecordedTestSanitizer> sanitizers)
        {
            List<BodyXmlSanitizer> batch = null;
            foreach (var sanitizer in sanitizers)
            {
                if (sanitizer.GetType() == typeof(BodyXmlSanitizer) && sanitizer.Condition == null &&
                    ((BodyXmlSanitizer)sanitizer)._batchedSanitizers == null)
                {
                    batch ??= new List<BodyXmlSanitizer>();
                    batch.Add((BodyXmlSanitizer)sanitizer);
                    continue;
                }

                if (batch != null)
                {
                    yield return batch.Count == 1 ? batch[0] : new BodyXmlSanitizer(batch);
                    batch = null;
                }

                yield return sanitizer;
            }

            if (batch != null)
            {
                yield return batch.Count == 1 ? batch[0] : new BodyXmlSanitizer(batch);
            }
        }

        public override string SanitizeTextBody(string contentType, string body)
        {
            ReadOnlySpan<char> mediaType = contentType.AsSpan();
            int separator = mediaType.IndexOf(';');
            if (separator >= 0)
            {
                mediaType = mediaType[..separator];
            }
            mediaType = mediaType.Trim();
            if ((!mediaType.Equals("application/xml", StringComparison.OrdinalIgnoreCase) &&
                !mediaType.Equals("text/xml", StringComparison.OrdinalIgnoreCase) &&
                !mediaType.EndsWith("+xml", StringComparison.OrdinalIgnoreCase)) || string.IsNullOrWhiteSpace(body))
            {
                return body;
            }

            bool hasPreamble = body[0] == '\uFEFF';
            var document = new XmlDocument { PreserveWhitespace = true, XmlResolver = null };
            try
            {
                using var textReader = new StringReader(body);
                if (hasPreamble)
                {
                    textReader.Read();
                }
                using var reader = XmlReader.Create(textReader, new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null
                });
                document.Load(reader);
            }
            catch (XmlException exception)
            {
                throw new HttpException(HttpStatusCode.BadRequest, $"Unable to sanitize XML body: {exception.Message}");
            }

            bool matched = false;
            if (_batchedSanitizers == null)
            {
                matched = SanitizeXmlBody(document);
            }
            else
            {
                foreach (var sanitizer in _batchedSanitizers)
                {
                    matched |= sanitizer.SanitizeXmlBody(document);
                }
            }

            if (!matched)
            {
                return body;
            }

            return hasPreamble ? "\uFEFF" + document.OuterXml : document.OuterXml;
        }

        private bool SanitizeXmlBody(XmlDocument document)
        {
            var selected = document.CreateNavigator().Select(_expression);
            var nodes = new List<XPathNavigator>();
            while (selected.MoveNext())
            {
                nodes.Add(selected.Current.Clone());
            }

            bool matched = false;
            foreach (var node in nodes)
            {
                if (node.NodeType == XPathNodeType.Element && node.SelectChildren(XPathNodeType.Element).MoveNext())
                {
                    continue;
                }

                if (node.NodeType == XPathNodeType.Element || node.NodeType == XPathNodeType.Attribute ||
                    node.NodeType == XPathNodeType.Text || node.NodeType == XPathNodeType.Whitespace ||
                    node.NodeType == XPathNodeType.SignificantWhitespace)
                {
                    var source = ((IHasXmlNode)node).GetNode();
                    if (source is XmlCDataSection || source is XmlWhitespace || source is XmlSignificantWhitespace)
                    {
                        if (source.ParentNode == null)
                        {
                            continue;
                        }

                        var text = document.CreateTextNode(source.Value);
                        source.ParentNode.ReplaceChild(text, source);
                        node.MoveTo(text.CreateNavigator());
                    }

                    node.SetValue(_newValue);
                    matched = true;
                }
            }

            return matched;
        }
    }
}