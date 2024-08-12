using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Text.Json.Serialization;
using System.Text.Json;
using System;
using APIView.Model.V2;

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
        NonBreakingSpace = 2,
        /// <summary>
        /// 4 NonBreakingSpaces.
        /// </summary>
        TabSpace = 3,
        /// <summary>
        /// Use this between method parameters. Depending on user setting this would result in a single space or new line.
        /// </summary>
        ParameterSeparator = 4
    }

    public enum DiffKind
    {
        NoneDiff = 0,
        Unchanged = 1, // Unchanged means the top level node is the same, the children could still contain diffs.
        Added = 2,
        Removed = 3
    }


    /// <summary>
    /// Represents an APIView token its properties and tags for APIView parsers.
    /// </summary>

    public class StructuredToken
    {
        /// <summary>
        /// Property key to indicate that a range of tokens is a group
        /// </summary>
        public static string GROUP_ID = "GroupId";
        /// <summary>
        /// Property key to indicate id to be navigated to when a token is clicked.
        /// </summary>
        public static string NAVIGATE_TO_ID = "NavigateToId";
        /// <summary>
        /// Property key to indicate that a token should be ignored for computing diff
        /// </summary>
        public static string SKIPP_DIFF = "SkippDiff";
        /// <summary>
        /// Property value that marks a token as documentation
        /// </summary>
        public static string DOCUMENTATION = "doc";
        /// <summary>
        /// Style class for text
        /// </summary>
        public static string TEXT = "text";
        /// <summary>
        /// Style class for keyword
        /// </summary>
        public static string KEYWORD = "keyword";
        /// <summary>
        /// Style class for literal
        /// </summary>
        public static string LITERAL = "literal";
        /// <summary>
        /// Style class for member-name
        /// </summary>
        public static string MEMBER_NAME = "mname";
        /// <summary>
        /// Style class for punctuation
        /// </summary>
        public static string PUNCTUATION = "punc";
        /// <summary>
        /// Style class for string-literal
        /// </summary>
        public static string STRING_LITERAL = "sliteral";
        /// <summary>
        /// Style class for type-name
        /// </summary>
        public static string TYPE_NAME = "tname";
        /// <summary>
        /// Style class for comment
        /// </summary>
        public static string COMMENT = "comment";


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
        public StructuredToken(StructuredToken token)
        {
            Value = token.Value;
            Id = token.Id;
            Kind = token.Kind;
            foreach (var property in token.PropertiesObj)
            {
                PropertiesObj.Add(property.Key, property.Value);
            }
            foreach (var renderClass in token.RenderClassesObj)
            {
                RenderClassesObj.Add(renderClass);
            }
        }

        public StructuredToken(ReviewToken token)
        {
            Value = token.Value;
            RenderClassesObj = new HashSet<string>(token.RenderClasses);

            if (token.IsDeprecated == true)
            {
                TagsObj.Add("Deprecated");
            }

            if (!string.IsNullOrEmpty(token.NavigateToId))
            {
                PropertiesObj.Add("NavigateToId", token.NavigateToId);
            }

            if (token.IsDocumentation == true)
            {
                TagsObj.Add(StructuredToken.DOCUMENTATION);
            }
            string className = StructuredToken.TEXT;
            switch (token.Kind)
            {
                case TokenKind.Text:
                    className = StructuredToken.TEXT;
                    break;
                case TokenKind.Punctuation:
                    className = StructuredToken.PUNCTUATION;
                    break;
                case TokenKind.Keyword:
                    className = StructuredToken.KEYWORD;
                    break;
                case TokenKind.TypeName:
                    className = StructuredToken.TYPE_NAME;
                    break;
                case TokenKind.MemberName:
                    className = StructuredToken.MEMBER_NAME;
                    break;
                case TokenKind.Comment:
                    className = StructuredToken.COMMENT;
                    break;
                case TokenKind.StringLiteral:
                    className = StructuredToken.STRING_LITERAL;
                    break;
                case TokenKind.Literal:
                    className = StructuredToken.LITERAL;
                    break;
            }
            RenderClassesObj.Add(className);
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
            token.RenderClassesObj.Add(TEXT);
            return token;
        }

        public static StructuredToken CreateKeywordToken(string value)
        {
            var token = new StructuredToken(value);
            token.RenderClassesObj.Add(KEYWORD);
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
            token.RenderClassesObj.Add(PUNCTUATION);
            return token;
        }

        public static StructuredToken CreatePunctuationToken(SyntaxKind syntaxKind)
        {
            return CreatePunctuationToken(SyntaxFacts.GetText(syntaxKind));
        }

        public static StructuredToken CreateTypeNameToken(string value)
        {
            var token = new StructuredToken(value);
            token.RenderClassesObj.Add(TYPE_NAME);
            return token;
        }

        public static StructuredToken CreateMemberNameToken(string value)
        {
            var token = new StructuredToken(value);
            token.RenderClassesObj.Add(MEMBER_NAME);
            return token;
        }

        public static StructuredToken CreateLiteralToken(string value)
        {
            var token = new StructuredToken(value);
            token.RenderClassesObj.Add(LITERAL);
            return token;
        }

        public static StructuredToken CreateStringLiteralToken(string value)
        {
            var token = new StructuredToken(value);
            token.RenderClassesObj.Add(STRING_LITERAL);
            return token;
        }
    }
}
