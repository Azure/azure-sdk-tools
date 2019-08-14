using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Text;

namespace ApiView
{
    public class TypeReferenceApiv
    {
        public bool IsString { get; set; }
        public TokenApiv[] Tokens { get; set; }

        public TypeReferenceApiv() { }

        public TypeReferenceApiv(TokenApiv[] tokens)
        {
            this.Tokens = tokens;
            this.IsString = false;
        }

        public TypeReferenceApiv(ISymbol symbol)
        {
            var tokens = new List<TokenApiv>();
            foreach (var part in symbol.ToDisplayParts())
            {
                tokens.Add(new TokenApiv(part));
            }
            this.Tokens = tokens.ToArray();
            this.IsString = (symbol is ITypeSymbol typeSymbol) && typeSymbol.SpecialType == SpecialType.System_String;
        }
      
        public enum TokenType
        {
            BuiltInType, ClassType, EnumType, TypeArgument, Punctuation, ValueType
        }

        public string ToDisplayString()
        {
            var returnString = new StringBuilder();
            var renderer = new TextRendererApiv();
            renderer.Render(this, returnString);
            return returnString.ToString();
        }
    }
}
