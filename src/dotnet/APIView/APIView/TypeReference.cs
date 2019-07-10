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
            this.IsString = this.Tokens.Last().IsString;
            this.Type = this.Tokens.Last().Type;
        }

        public TypeReference(IFieldSymbol symbol)
        {
            var tokens = new List<Token>();
            foreach (var part in symbol.Type.ToDisplayParts())
            {
                tokens.Add(new Token(part));
            }
            this.Tokens = tokens.ToArray();
            this.Type = this.Tokens.Last().Type;
            this.IsString = this.Tokens.Last().IsString;
        }

        public TypeReference(IMethodSymbol symbol)
        {
            var tokens = new List<Token>();
            foreach (var part in symbol.ReturnType.ToDisplayParts())
            {
                tokens.Add(new Token(part));
            }
            this.Tokens = tokens.ToArray();
            this.Type = this.Tokens.Last().Type;
            this.IsString = this.Tokens.Last().IsString;
        }

        public TypeReference(IParameterSymbol symbol)
        {
            var tokens = new List<Token>();
            foreach (var part in symbol.ToDisplayParts())
            {
                tokens.Add(new Token(part));
            }
            this.Tokens = tokens.ToArray();
            this.Type = this.Tokens.Last().Type;
            this.IsString = this.Tokens.Last().IsString;
        }

        public TypeReference(IPropertySymbol symbol)
        {
            var tokens = new List<Token>();
            foreach (var part in symbol.Type.ToDisplayParts())
            {
                tokens.Add(new Token(part));
            }
            this.Tokens = tokens.ToArray();
            this.Type = this.Tokens.Last().Type;
            this.IsString = this.Tokens.Last().IsString;
        }

        public enum TypeName
        {
            SpecialType, BuiltInType, Punctuation, NullType
        }
    }
}
