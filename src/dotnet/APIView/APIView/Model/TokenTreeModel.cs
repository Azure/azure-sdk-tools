using System;
using System.Collections.Generic;
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

    [JsonObject("st")]
    public class StructuredToken
    {
        [JsonProperty("t")]
        private HashSet<string> _tagsForSerializer
        {
            get { return Tags.Count > 0 ? Tags : null; }
            set { Tags = value ?? new HashSet<string>(); }
        }

        [JsonProperty("p")]
        private Dictionary<string, string> _propertiesForSerializer
        {
            get { return Properties.Count > 0 ? Properties : null; }
            set { Properties = value ?? new Dictionary<string, string>(); }
        }

        [JsonProperty("rc")]
        private HashSet<string> _renderClassesForSerializer
        {
            get { return RenderClasses.Count > 0 ? RenderClasses : null; }
            set { RenderClasses = value ?? new HashSet<string>(); }
        }


        [JsonProperty("v")]
        public string Value { get; set; } = string.Empty;
        [JsonProperty("i")]
        public string Id { get; set; }
        [JsonProperty("k")]
        public StructuredTokenKind Kind { get; set; }
        [JsonIgnore]
        public HashSet<string> Tags { get; set; } = new HashSet<string>();
        [JsonIgnore]
        public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();
        [JsonIgnore]
        public HashSet<string> RenderClasses { get; set; } = new HashSet<string>();
        
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
            foreach (var property in token.Properties)
            {
                Properties.Add(property.Key, property.Value);
            }
            foreach (var renderClass in token.RenderClasses)
            {
                token.RenderClasses.Add(renderClass);
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
            token.RenderClasses.Add("punc");
            return token;
        }

        public static StructuredToken CreatePunctuationToken(SyntaxKind syntaxKind)
        {
            return CreatePunctuationToken(SyntaxFacts.GetText(syntaxKind));
        }

        public static StructuredToken CreateTypeNameToken(string value)
        {
            var token = new StructuredToken(value);
            token.RenderClasses.Add("tname");
            return token;
        }

        public static StructuredToken CreateMemberNameToken(string value)
        {
            var token = new StructuredToken(value);
            token.RenderClasses.Add("mname");
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
            token.RenderClasses.Add("sliteral");
            return token;
        }
    }

    [JsonObject("at")]

    public class APITreeNode
    {
        [JsonProperty("t")]
        private HashSet<string> _tagsForSerializer
        {
            get { return Tags.Count > 0 ? Tags : null; }
            set { Tags = value ?? new HashSet<string>(); }
        }

        [JsonProperty("p")]
        private Dictionary<string, string> _propertiesForSerializer
        {
            get { return Properties.Count > 0 ? Properties : null; }
            set { Properties = value ?? new Dictionary<string, string>(); }
        }

        [JsonProperty("tt")]
        private List<StructuredToken> _topTokensForSerializer
        {
            get { return TopTokens.Count > 0 ? TopTokens : null; }
            set { TopTokens = value ?? new List<StructuredToken>(); }
        }

        [JsonProperty("bt")]
        private List<StructuredToken> _bottomTokensForSerializer
        {
            get { return BottomTokens.Count > 0 ? BottomTokens : null; }
            set { BottomTokens = value ?? new List<StructuredToken>(); }
        }

        [JsonProperty("c")]
        private List<APITreeNode> _childrenForSerializer
        {
            get { return Children.Count > 0 ? Children : null; }
            set { Children = value ?? new List<APITreeNode>(); }
        }

        [JsonProperty("n")]
        public string Name { get; set; }
        [JsonProperty("i")]
        public string Id { get; set; }
        [JsonProperty("k")]
        public string Kind { get; set; }
        [JsonIgnore]
        public HashSet<string> Tags { get; set; } = new HashSet<string>();
        [JsonIgnore]
        public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();
        [JsonIgnore]
        public List<StructuredToken> TopTokens { get; set; } = new List<StructuredToken>();
        [JsonIgnore]
        public List<StructuredToken> BottomTokens { get; set; } = new List<StructuredToken>();
        [JsonIgnore]
        public List<APITreeNode> Children { get; set; } = new List<APITreeNode>();
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
