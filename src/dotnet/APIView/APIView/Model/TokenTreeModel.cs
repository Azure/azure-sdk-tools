using System;
using System.Collections;
using System.Collections.Generic;
using ApiView;
using APIView.DIff;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace APIView.Model
{
    public enum StructuredTokenKind
    {
        Content = 0,
        LineBreak = 1,
        NoneBreakingSpace = 2,
        TabSpace = 3,
        ParameterSeparator = 4,
        Url = 5
    }

    public enum DiffKind
    {
        NoneDiff = 0,
        Unchanged = 1, // Unchanged means the top level node is the same, the children could still contain diffs.
        Added = 2,
        Removed = 3
    }

    public class StructuredTokenConverter : JsonConverter
    {
        private readonly string _parameterSeparator;

        public StructuredTokenConverter(string parameterSeparator = "\u00A0")
        {
            _parameterSeparator = parameterSeparator;
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(StructuredToken);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            JToken t = JToken.FromObject(value);
            t.WriteTo(writer);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject jObject = JObject.Load(reader);
            StructuredToken myObject = jObject.ToObject<StructuredToken>();

            switch (myObject.Kind)
            {
                case StructuredTokenKind.LineBreak:
                    myObject.Value = "\u000A";
                    break;
                case StructuredTokenKind.NoneBreakingSpace:
                    myObject.Value = "\u00A0";
                    break;
                case StructuredTokenKind.TabSpace:
                    myObject.Value = "\u0009";
                    break;
                case StructuredTokenKind.ParameterSeparator:
                    myObject.Value = _parameterSeparator;
                    break;
            }
            return myObject;
        }
    }

    public class StructuredToken
    {
        public string Value { get; set; } = string.Empty;
        public string Id { get; set; }
        public StructuredTokenKind Kind { get; set; }
        public Dictionary<string, string> Properties { get; } = new Dictionary<string, string>();
        public HashSet<string> RenderClasses { get; } = new HashSet<string>();

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

        public static StructuredToken CreateEmptyToken()
        {
            var token = new StructuredToken();
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

        public static StructuredToken CreateTextToken(string value)
        {
            var token = new StructuredToken(value);
            token.RenderClasses.Add("text");
            return token;
        }

        public static StructuredToken CreateKeywordToken(string value)
        {
            var token = new StructuredToken(value);
            token.RenderClasses.Add("keyword");
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
            token.RenderClasses.Add("punctuation");
            return token;
        }

        public static StructuredToken CreatePunctuationToken(SyntaxKind syntaxKind)
        {
            return CreatePunctuationToken(SyntaxFacts.GetText(syntaxKind));
        }

        public static StructuredToken CreateTypeNameToken(string value)
        {
            var token = new StructuredToken(value);
            token.RenderClasses.Add("type-name");
            return token;
        }

        public static StructuredToken CreateMemberNameToken(string value)
        {
            var token = new StructuredToken(value);
            token.RenderClasses.Add("member-name");
            return token;
        }

        public static StructuredToken CreateLiteralToken(string value)
        {
            var token = new StructuredToken(value);
            token.RenderClasses.Add("literal");
            return token;
        }

        public static StructuredToken CreateStringLiteralToken(string value)
        {
            var token = new StructuredToken(value);
            token.RenderClasses.Add("string-literal");
            return token;
        }
    }

    public class StructuredTokenForAPI : StructuredToken
    {
        public StructuredTokenForAPI(StructuredToken token)
        {
            Value = token.Value;
            Id = token.Id;
            Kind = token.Kind;
            foreach (var property in token.Properties)
            {
                Properties.Add(property.Key, property.Value);
            }
            foreach (var renderClass in token.RenderClasses)
            {
                RenderClasses.Add(renderClass);
            }
        }
        public DiffKind DiffKind { get; set; }
    }

    public class APITreeNode
    {
        public string Name { get; set; }
        public string Id { get; set; }
        public string Kind { get; set; }
        public HashSet<string> Tags { get; set; } = new HashSet<string>(); // Use for hidden and Deprecated
        public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();
        public List<StructuredToken> TopTokens { get; set; } = new List<StructuredToken>();
        public List<StructuredToken> BottomTokens { get; set; } = new List<StructuredToken>();
        public List<APITreeNode> Children { get; set; } = new List<APITreeNode>();
    }

    public class  APITreeNodeForAPI : APITreeNode
    {
        public new List<APITreeNodeForAPI> Children { get; set; } = new List<APITreeNodeForAPI>();
        public DiffKind DiffKind { get; set; } = DiffKind.NoneDiff;
        public List<StructuredToken> TopDiffTokens { get; set; } = new List<StructuredToken>();
        public List<StructuredToken> BottomDiffTokens { get; set; } = new List<StructuredToken>();

        public override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 23 + (Name != null ? Name.GetHashCode() : 0);
            hash = hash * 23 + (Id != null ? Id.GetHashCode() : 0);
            hash = hash * 23 + (Kind != null ? Kind.GetHashCode() : 0);
            return hash;
        }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            var other = (APITreeNodeForAPI)obj;
            return Name == other.Name && Id == other.Id && Kind == other.Kind;
        }
    }
}
