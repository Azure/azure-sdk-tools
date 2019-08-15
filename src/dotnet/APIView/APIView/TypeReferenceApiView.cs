using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Text;

namespace ApiView
{
    public class TypeReferenceApiView
    {
        public bool IsString { get; set; }
        public TokenApiView[] Tokens { get; set; }

        public TypeReferenceApiView() { }

        public TypeReferenceApiView(TokenApiView[] tokens)
        {
            this.Tokens = tokens;
            this.IsString = false;
        }

        public TypeReferenceApiView(ISymbol symbol)
        {
            var tokens = new List<TokenApiView>();
            foreach (var part in symbol.ToDisplayParts())
            {
                tokens.Add(new TokenApiView(part));
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
            var renderer = new TextRendererApiView();
            renderer.Render(this, returnString);
            return returnString.ToString();
        }
    }
}
