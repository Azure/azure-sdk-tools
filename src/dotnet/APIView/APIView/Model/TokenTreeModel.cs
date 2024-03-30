using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace APIView.Model
{
    public class StructuredToken
    {
        public Dictionary<string, string> Properties { get; } = new Dictionary<string, string>();
        public HashSet<string> RenderClasses { get; } = new HashSet<string>();

        public StructuredToken(string value)
        {
            Properties.Add("Value", value);
        }

        public static StructuredToken CreateLineBreakToken()
        {
            var token = new StructuredToken("\n");
            token.Properties.Add("Kind", "LineBreak");
            return token;
        }

        public static StructuredToken CreateSpaceToken()
        {
            var token = new StructuredToken("\u0020");
            token.Properties.Add("Kind", "Space");
            return token;
        }

        public static StructuredToken CreateTextToken(string value)
        {
            var token = new StructuredToken(value);
            token.RenderClasses.Add("Text");
            return token;
        }

        public static StructuredToken CreateKeywordToken(string value)
        {
            var token = new StructuredToken(value);
            token.RenderClasses.Add("Keyword");
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
            token.RenderClasses.Add("Punctuation");
            return token;
        }

        public static StructuredToken CreatePunctuationToken(SyntaxKind syntaxKind)
        {
            return CreatePunctuationToken(SyntaxFacts.GetText(syntaxKind));
        }

        public static StructuredToken CreateTypeNameToken(string value)
        {
            var token = new StructuredToken(value);
            token.RenderClasses.Add("TypeName");
            return token;
        }

        public static StructuredToken CreateMemberNameToken(string value)
        {
            var token = new StructuredToken(value);
            token.RenderClasses.Add("MemberName");
            return token;
        }

        public static StructuredToken CreateLiteralToken(string value)
        {
            var token = new StructuredToken(value);
            token.RenderClasses.Add("Literal");
            return token;
        }

        public static StructuredToken CreateStringLiteralToken(string value)
        {
            var token = new StructuredToken(value);
            token.RenderClasses.Add("StringLiteral");
            return token;
        }

        public static StructuredToken CreateParameterSeparatorToken()
        {
            var token = new StructuredToken("\u0020");
            token.Properties.Add("Kind", "ParamSeparator");
            return token;
        }
    }

    public class APITreeNode
    {
        public Dictionary<string, string> Properties { get; } = new Dictionary<string, string>();
        public List<StructuredToken> TopTokens { get; } = new List<StructuredToken>();
        public List<StructuredToken> BottomTokens { get; } = new List<StructuredToken>();
        public List<APITreeNode> Children { get; } = new List<APITreeNode>();
    }
}
