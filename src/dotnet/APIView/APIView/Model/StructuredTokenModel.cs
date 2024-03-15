using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Text.Json.Serialization;

namespace APIView.TreeToken
{
    /// <summary>
    /// Represents the type of a structured token.
    /// All tokens should be content except for spacing tokens and url.
    /// </summary>
    public enum StructuredTokenKind
    {
        /// <summary>
        /// Specifies that the token is content.
        /// This is the default value for a token.
        /// </summary>
        Content = 0,
        /// <summary>
        /// Space token indicating switch to new line.
        /// </summary>
        LineBreak = 1,
        /// <summary>
        /// Regular single space.
        /// </summary>
        NoneBreakingSpace = 2,
        /// <summary>
        /// 4 NonBreakingSpaces.
        /// </summary>
        TabSpace = 3,
        /// <summary>
        /// Use this between method parameters. Depending on user setting this would result in a single space or new line.
        /// </summary>
        ParameterSeparator = 4,
        /// <summary>
        /// A url token should have `LinkText` property i.e `token.Properties["LinkText"]` and the url/link should be the token value.
        /// </summary>
        Url = 5
    }

    /// <summary>
    /// Represents an APIView token its properties and tags for APIView parsers.
    /// </summary>

    public class StructuredToken
    {
        /// <summary>
        /// Token Id.
        /// Previously known as DefinitionId.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Represents the type of a structured token.
        /// </summary>
        public StructuredTokenKind Kind { get; set; } = StructuredTokenKind.Content;

        /// <summary>
        /// The token value which will be displayed. Spacing tokens don't need to have value as it will be
        /// replaced at deserialization based on the Token Kind.
        /// </summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// Properties of the token.
        /// <list type="bullet">
        /// <item>
        /// <description>GroupId: `doc` to group consecutive comment tokens as documentation.</description>
        /// </item>
        /// <item>
        /// <description>NavigateToId: When the token is clicked, will navigate to the location that matches the provided token id.</description>
        /// </item>
        /// </list>
        /// </summary>
        public Dictionary<string, string> Properties
        {
            get { return PropertiesObj.Count > 0 ? PropertiesObj : null; }
            set { PropertiesObj = value ?? new Dictionary<string, string>(); }
        }

        /// <summary>
        /// List of default CSS configuration for any language.
        /// Languages can override these or add new classes by reaching out to the APIView team.
        /// <list type="bullet">
        /// <item>
        /// <description>comment</description>
        /// </item>
        /// <item>
        /// <description>keyword</description>
        /// </item>
        /// <item>
        /// <description>literal</description>
        /// </item>
        /// <item>
        /// <description>mname: member name</description>
        /// </item>
        /// <item>
        /// <description>punc</description>
        /// </item>
        /// <item>
        /// <description>sliteral: string literal</description>
        /// </item>
        /// <item>
        /// <description>text</description>
        /// </item>
        /// <item>
        /// <description>tname: type name</description>
        /// </item>
        /// </list>
        /// </summary>
        public HashSet<string> RenderClasses
        {
            get { return RenderClassesObj.Count > 0 ? RenderClassesObj : null; }
            set { RenderClassesObj = value ?? new HashSet<string>(); }
        }

        /// <summary>
        /// Behavioral boolean properties
        /// <list type="bullet">
        /// <item>
        /// <description>Deprecated: Mark a node as deprecated</description>
        /// </item>
        /// <item>
        /// <description>SkipDiff: Indicate that a node should not be used in computation of diff.</description>
        /// </item>
        /// </list>
        /// </summary>
        public HashSet<string> Tags
        {
            get { return TagsObj.Count > 0 ? TagsObj : null; }
            set { TagsObj = value ?? new HashSet<string>(); }
        }

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
            token.Kind = StructuredTokenKind.NoneBreakingSpace;
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
