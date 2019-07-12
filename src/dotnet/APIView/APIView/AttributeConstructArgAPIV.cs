using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace APIView
{
    public class AttributeConstructArgAPIV
    {
        public TokenAPIV[] Tokens { get; set; }

        public AttributeConstructArgAPIV() { }

        public AttributeConstructArgAPIV(TokenAPIV[] tokens)
        {
            this.Tokens = tokens;
        }

        public AttributeConstructArgAPIV(TypedConstant value)
        {
            if (value.Type.Name.Equals("String"))
                this.Tokens = new TokenAPIV[] { new TokenAPIV("\"" + value.Value.ToString() + "\"", TypeReferenceAPIV.TokenType.ValueType) };
            else
                this.Tokens = new TokenAPIV[] { new TokenAPIV(value.Value.ToString(), TypeReferenceAPIV.TokenType.ValueType) };
        }

        public AttributeConstructArgAPIV(KeyValuePair<string, TypedConstant> pair)
        {
            var tokens = new List<TokenAPIV>
            {
                new TokenAPIV(pair.Key, TypeReferenceAPIV.TokenType.Punctuation),
                new TokenAPIV(" ", TypeReferenceAPIV.TokenType.Punctuation),
                new TokenAPIV("=", TypeReferenceAPIV.TokenType.Punctuation),
                new TokenAPIV(" ", TypeReferenceAPIV.TokenType.Punctuation)
            };
            if (pair.Value.Type.SpecialType == SpecialType.System_String)
            {
                tokens.Add(new TokenAPIV("\"", TypeReferenceAPIV.TokenType.ValueType));
                tokens.Add(new TokenAPIV(pair.Value.Value.ToString(), TypeReferenceAPIV.TokenType.ValueType));
                tokens.Add(new TokenAPIV("\"", TypeReferenceAPIV.TokenType.ValueType));
            }
            else
                tokens.Add(new TokenAPIV(pair.Value.Value.ToString(), TypeReferenceAPIV.TokenType.ValueType));
            this.Tokens = tokens.ToArray();
        }
    }
}
