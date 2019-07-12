using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Text;

namespace APIView
{
    public class TypeReferenceAPIV
    {
        public bool IsString { get; set; }
        public TokenAPIV[] Tokens { get; set; }

        public TypeReferenceAPIV() { }

        public TypeReferenceAPIV(TokenAPIV[] tokens)
        {
            this.Tokens = tokens;
            this.IsString = false;
        }

        public TypeReferenceAPIV(ISymbol symbol)
        {
            var tokens = new List<TokenAPIV>();
            foreach (var part in symbol.ToDisplayParts())
            {
                tokens.Add(new TokenAPIV(part));
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
            var renderer = new TextRendererAPIV();
            renderer.Render(this, returnString);
            return returnString.ToString();
        }
    }
}
