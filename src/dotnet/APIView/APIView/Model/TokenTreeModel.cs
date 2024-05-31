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
        private HashSet<string> _tags;
        private HashSet<string> _renderClasses;
        private Dictionary<string, string> _properties;

        [JsonProperty("v")]
        public string Value { get; set; } = string.Empty;
        [JsonProperty("i")]
        public string Id { get; set; }
        [JsonProperty("k")]
        public StructuredTokenKind Kind { get; set; }
        [JsonProperty("t")]
        public HashSet<string> Tags 
        {   
            get 
            {
                if (_tags == null)
                {
                    _tags = new HashSet<string>();
                }
                return _tags;
            }
            set 
            {
                _tags = value;
            } 
        }
        [JsonProperty("p")]
        public Dictionary<string, string> Properties
        {
            get
            {
                if (_properties == null)
                {
                    _properties = new Dictionary<string, string>();
                }
                return _properties;
            }
            set
            {
                _properties = value;
            }
        }
        [JsonProperty("rc")]
        public HashSet<string> RenderClasses
        {
            get
            {
                if (_renderClasses == null)
                {
                    _renderClasses = new HashSet<string>();
                }
                return _renderClasses;
            }
            set
            {
                _renderClasses = value;
            }
        }

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

    [JsonObject("at")]

    public class APITreeNode
    {
        [JsonProperty("n")]
        public string Name { get; set; }
        [JsonProperty("i")]
        public string Id { get; set; }
        [JsonProperty("k")]
        public string Kind { get; set; }
        [JsonProperty("t")]
        public HashSet<string> Tags { get; set; }= new HashSet<string>();
        [JsonProperty("p")]
        public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();

        [JsonProperty("tt")]
        public List<StructuredToken> TopTokens { get; set; } = new List<StructuredToken>();

        [JsonProperty("bt")]
        public List<StructuredToken> BottomTokens { get; set; } = new List<StructuredToken>();

        [JsonProperty("c")]
        public List<APITreeNode> Children { get; set; } = new List<APITreeNode>();
        [JsonProperty("dk")]
        public DiffKind DiffKind { get; set; } = DiffKind.NoneDiff;
        [JsonProperty("tdt")]
        public List<StructuredToken> TopDiffTokens { get; set; } = new List<StructuredToken>();

        [JsonProperty("bdt")]
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
