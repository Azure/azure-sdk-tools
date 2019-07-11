using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace APIView
{
    public class TypeReference
    {
        public bool IsString { get; set; }
        public Token[] Tokens { get; set; }
        public TypeName Type { get; set; }

        public TypeReference()
        {
            this.IsString = false;
            this.Type = TypeName.NullType;
        }

        public TypeReference(Token[] tokens)
        {
            this.Tokens = tokens;
            this.IsString = false;
            this.Type = this.Tokens.Last().Type;
        }

        public TypeReference(ISymbol symbol)
        {
            var tokens = new List<Token>();
            foreach (var part in symbol.ToDisplayParts())
            {
                tokens.Add(new Token(part));
            }
            this.Tokens = tokens.ToArray();
            this.Type = this.Tokens.Last().Type;
        }

        public TypeReference(INamedTypeSymbol symbol)
        {
            var tokens = new List<Token>();
            if (symbol.EnumUnderlyingType != null)
            {
                foreach (var part in symbol.EnumUnderlyingType.ToDisplayParts())
                {
                    tokens.Add(new Token(part));
                }
            }
            else
            {
                foreach(var part in symbol.ToDisplayParts())
                {
                    tokens.Add(new Token(part));
                }
            }
            this.Tokens = tokens.ToArray();
            this.Type = this.Tokens.Last().Type;
            this.IsString = symbol.SpecialType == SpecialType.System_String;
        }

        public enum TypeName
        {
            BuiltInType, ClassType, EnumType, SpecialType, Punctuation, NullType, ValueType
        }
    }
}
