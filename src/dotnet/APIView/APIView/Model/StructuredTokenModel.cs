using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Text.Json.Serialization;

namespace APIView.TreeToken
{
    public enum StructuredTokenKind
    {
        Content = 0,
        LineBreak = 1,
        NonBreakingSpace = 2,
        TabSpace = 3,
        ParameterSeparator = 4,
        Url = 5
    }

    /// <summary>
    /// Used to represent a APIView token its properties and tags for APIView parsers.
    /// </summary>

    public class StructuredToken
    {
        public HashSet<string> Tags
        {
            get { return TagsObj.Count > 0 ? TagsObj : null; }
            set { TagsObj = value ?? new HashSet<string>(); }
        }
        public Dictionary<string, string> Properties
        {
            get { return PropertiesObj.Count > 0 ? PropertiesObj : null; }
            set { PropertiesObj = value ?? new Dictionary<string, string>(); }
        }
        public HashSet<string> RenderClasses
        {
            get { return RenderClassesObj.Count > 0 ? RenderClassesObj : null; }
            set { RenderClassesObj = value ?? new HashSet<string>(); }
        }
        public string Value { get; set; } = string.Empty;
        public string Id { get; set; }
        public StructuredTokenKind Kind { get; set; } = StructuredTokenKind.Content;
        [JsonIgnore]
        public HashSet<string> TagsObj { get; set; } = new HashSet<string>();
        [JsonIgnore]
        public Dictionary<string, string> PropertiesObj { get; set; } = new Dictionary<string, string>();
        [JsonIgnore]
        public HashSet<string> RenderClassesObj { get; set; } = new HashSet<string>();

        public StructuredToken()
        {
            new StructuredToken(string.Empty);
        }

        public StructuredToken(string value)
        {
            Value = value;
            Kind = StructuredTokenKind.Content;
        }

        public static StructuredToken CreateLineBreakToken()
        {
            var token = new StructuredToken();
            token.Kind = StructuredTokenKind.LineBreak;
            return token;
        }

        public static StructuredToken CreateEmptyToken(string id = null)
        {
            var token = new StructuredToken();
            token.Id = id;
            token.Kind = StructuredTokenKind.Content;
            return token;
        }

        public static StructuredToken CreateSpaceToken()
        {
            var token = new StructuredToken();
            token.Kind = StructuredTokenKind.NonBreakingSpace;
            return token;
        }

        public static StructuredToken CreateParameterSeparatorToken()
        {
            var token = new StructuredToken();
            token.Kind = StructuredTokenKind.ParameterSeparator;
            return token;
        }

        public static StructuredToken CreateTextToken(string value, string id = null)
        {
            var token = new StructuredToken(value);
            if (!string.IsNullOrEmpty(id))
            {
                token.Id = id;
            }
            token.RenderClassesObj.Add("text");
            return token;
        }

        public static StructuredToken CreateKeywordToken(string value)
        {
            var token = new StructuredToken(value);
            token.RenderClassesObj.Add("keyword");
            return token;
        }

        public static StructuredToken CreateKeywordToken(SyntaxKind syntaxKind)
        {
            return CreateKeywordToken(SyntaxFacts.GetText(syntaxKind));
        }

        public static StructuredToken CreateKeywordToken(Accessibility accessibility)
        {
            return CreateKeywordToken(SyntaxFacts.GetText(accessibility));
        }

        public static StructuredToken CreatePunctuationToken(string value)
        {
            var token = new StructuredToken(value);
            token.RenderClassesObj.Add("punc");
            return token;
        }

        public static StructuredToken CreatePunctuationToken(SyntaxKind syntaxKind)
        {
            return CreatePunctuationToken(SyntaxFacts.GetText(syntaxKind));
        }

        public static StructuredToken CreateTypeNameToken(string value)
        {
            var token = new StructuredToken(value);
            token.RenderClassesObj.Add("tname");
            return token;
        }

        public static StructuredToken CreateMemberNameToken(string value)
        {
            var token = new StructuredToken(value);
            token.RenderClassesObj.Add("mname");
            return token;
        }

        public static StructuredToken CreateLiteralToken(string value)
        {
            var token = new StructuredToken(value);
            token.RenderClassesObj.Add("literal");
            return token;
        }

        public static StructuredToken CreateStringLiteralToken(string value)
        {
            var token = new StructuredToken(value);
            token.RenderClassesObj.Add("sliteral");
            return token;
        }
    }
}
