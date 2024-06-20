using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace APIView.Model
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

    public enum DiffKind
    {
        NoneDiff = 0,
        Unchanged = 1, // Unchanged means the top level node is the same, the children could still contain diffs.
        Added = 2,
        Removed = 3
    }

    public class StructuredTokenConverter : JsonConverter<StructuredToken>
    {
        private readonly string _parameterSeparator;

        public StructuredTokenConverter(string parameterSeparator = "\u00A0")
        {
            _parameterSeparator = parameterSeparator;
        }

        public override StructuredToken Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var jObject = JsonDocument.ParseValue(ref reader).RootElement;
            var myObject = JsonSerializer.Deserialize<StructuredToken>(jObject.GetRawText());

            switch (myObject.Kind)
            {
                case StructuredTokenKind.LineBreak:
                    myObject.Value = "\u000A";
                    break;
                case StructuredTokenKind.NonBreakingSpace:
                    myObject.Value = "\u00A0";
                    break;
                case StructuredTokenKind.TabSpace:
                    myObject.Value = "\u00A0\u00A0\u00A0\u00A0";
                    break;
                case StructuredTokenKind.ParameterSeparator:
                    myObject.Value = _parameterSeparator;
                    break;
            }
            return myObject;
        }

        public override void Write(Utf8JsonWriter writer, StructuredToken value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value, options);
        }
    }

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
        public StructuredTokenKind Kind { get; set; }

        [JsonIgnore]
        public HashSet<string> TagsObj { get; set; } = new HashSet<string>();

        [JsonIgnore]
        public Dictionary<string, string> PropertiesObj { get; set; } = new Dictionary<string, string>();

        [JsonIgnore]
        public HashSet<string> RenderClassesObj { get; set; } = new HashSet<string>();
        
        public StructuredToken()
        {
            Value = string.Empty;
            Kind = StructuredTokenKind.Content;
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
            token.Kind = StructuredTokenKind.NonBreakingSpace;
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

    public class APITreeNode
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

        public List<StructuredToken> TopTokens
        {
            get { return TopTokensObj.Count > 0 ? TopTokensObj : null; }
            set { TopTokensObj = value ?? new List<StructuredToken>(); }
        }

        public List<StructuredToken> BottomTokens
        {
            get { return BottomTokensObj.Count > 0 ? BottomTokensObj : null; }
            set { BottomTokensObj = value ?? new List<StructuredToken>(); }
        }

        public List<APITreeNode> Children
        {
            get { return ChildrenObj.Count > 0 ? ChildrenObj : null; }
            set { ChildrenObj = value ?? new List<APITreeNode>(); }
        }
        public string Name { get; set; }
        public string Id { get; set; }
        public string Kind { get; set; }

        [JsonIgnore]
        public HashSet<string> TagsObj { get; set; } = new HashSet<string>();

        [JsonIgnore]
        public Dictionary<string, string> PropertiesObj { get; set; } = new Dictionary<string, string>();

        [JsonIgnore]
        public List<StructuredToken> TopTokensObj { get; set; } = new List<StructuredToken>();

        [JsonIgnore]
        public List<StructuredToken> BottomTokensObj { get; set; } = new List<StructuredToken>();

        [JsonIgnore]
        public List<APITreeNode> ChildrenObj { get; set; } = new List<APITreeNode>();

        [JsonIgnore]
        public DiffKind DiffKind { get; set; } = DiffKind.NoneDiff;

        [JsonIgnore]
        public List<StructuredToken> TopDiffTokens { get; set; } = new List<StructuredToken>();

        [JsonIgnore]
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

            var other = (APITreeNode)obj;
            return Name == other.Name && Id == other.Id && Kind == other.Kind;
        }
    }
}
