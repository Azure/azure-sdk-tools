using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Text;

namespace APIView
{
    public class TypeReference
    {
        public bool IsString { get; set; }
        public Token[] Tokens { get; set; }

        public TypeReference() { }

        public TypeReference(Token[] tokens)
        {
            this.Tokens = tokens;
            this.IsString = false;
        }

        public TypeReference(ISymbol symbol)
        {
            var tokens = new List<Token>();
            foreach (var part in symbol.ToDisplayParts())
            {
                tokens.Add(new Token(part));
            }
            this.Tokens = tokens.ToArray();
            this.IsString = (symbol is ITypeSymbol typeSymbol) && typeSymbol.SpecialType == SpecialType.System_String;
        }
      
        public enum TokenType
        {
            BuiltInType, ClassType, EnumType, TypeArgument, Punctuation
        }

        public string ToDisplayString()
        {
            var returnString = new StringBuilder();
            var renderer = new TextRendererAPIV();
            renderer.Render(this, returnString);
            return returnString.ToString();
        }
    }
}
