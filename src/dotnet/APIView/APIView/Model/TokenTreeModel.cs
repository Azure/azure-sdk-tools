using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace APIView.Model
{
    public enum StructuredTokenKind
    {
        LineBreak = 0,
        NoneBreakingSpace = 1,
        ParameterSeparator = 2,
        Content = 3,
    }


    public class StructuredToken
    {
        public string Value { get; set; }
        public string Id { get; set; }
        public string GroupId { get; set; }
        public StructuredTokenKind Kind { get; set; }
        public Dictionary<string, string> Properties { get; } = new Dictionary<string, string>();
        public HashSet<string> RenderClasses { get; } = new HashSet<string>();

        public StructuredToken(string value)
        {
            Value = value;
            Kind = StructuredTokenKind.Content;
        }

        public static StructuredToken CreateLineBreakToken()
        {
            var token = new StructuredToken("\n");
            token.Kind = StructuredTokenKind.LineBreak;
            return token;
        }

        public static StructuredToken CreateSpaceToken()
        {
            var token = new StructuredToken("\u0020");
            token.Kind = StructuredTokenKind.NoneBreakingSpace;
            return token;
        }

        public static StructuredToken CreateTextToken(string value)
        {
            var token = new StructuredToken(value);
            token.RenderClasses.Add("csText");
            return token;
        }

        public static StructuredToken CreateKeywordToken(string value)
        {
            var token = new StructuredToken(value);
            token.RenderClasses.Add("csKeyword");
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
            token.RenderClasses.Add("csPunctuation");
            return token;
        }

        public static StructuredToken CreatePunctuationToken(SyntaxKind syntaxKind)
        {
            return CreatePunctuationToken(SyntaxFacts.GetText(syntaxKind));
        }

        public static StructuredToken CreateTypeNameToken(string value)
        {
            var token = new StructuredToken(value);
            token.RenderClasses.Add("csTypeName");
            return token;
        }

        public static StructuredToken CreateMemberNameToken(string value)
        {
            var token = new StructuredToken(value);
            token.RenderClasses.Add("csMemberName");
            return token;
        }

        public static StructuredToken CreateLiteralToken(string value)
        {
            var token = new StructuredToken(value);
            token.RenderClasses.Add("csLiteral");
            return token;
        }

        public static StructuredToken CreateStringLiteralToken(string value)
        {
            var token = new StructuredToken(value);
            token.RenderClasses.Add("csStringLiteral");
            return token;
        }

        public static StructuredToken CreateParameterSeparatorToken()
        {
            var token = new StructuredToken("\u0020");
            token.Kind = StructuredTokenKind.ParameterSeparator;
            return token;
        }
    }

    public class APITreeNode
    {
        public string Name { get; set; }
        public string Id { get; set; }
        public string Kind { get; set; }
        public string SubKind { get; set; }
        public bool IsHidden { get; set; }
        public bool IsDeprecated { get; set; }
        public Dictionary<string, string> Properties { get; } = new Dictionary<string, string>();
        public List<StructuredToken> TopTokens { get; } = new List<StructuredToken>();
        public List<StructuredToken> BottomTokens { get; } = new List<StructuredToken>();
        public List<APITreeNode> Children { get; } = new List<APITreeNode>();
    }
}
