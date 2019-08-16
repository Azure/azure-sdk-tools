using Microsoft.CodeAnalysis;

namespace ApiView
{
    public class TokenApiView
    {
        public string Id { get; set; }
        public string DisplayString { get; set; }
        public bool IsNavigable { get; set; }
        
        public TypeReferenceApiView.TokenType Type { get; set; }

        public TokenApiView()
        {
            this.DisplayString = "";
        }

        public TokenApiView(SymbolDisplayPart part)
        {
            this.DisplayString = part.ToString();
            this.IsNavigable = false;
            if (part.Symbol == null)
                this.Id = "";
            else
            {
                var typeParamIndex = part.Symbol.ToDisplayString().LastIndexOf("<");
                if (typeParamIndex > 0)
                    this.Id = part.Symbol.ToDisplayString().Remove(typeParamIndex);
                else
                    this.Id = part.Symbol.ToDisplayString();
            }
            
            switch (part.Kind)
            {
                case SymbolDisplayPartKind.ClassName:
                case SymbolDisplayPartKind.ErrorTypeName:
                case SymbolDisplayPartKind.InterfaceName:
                case SymbolDisplayPartKind.StructName:
                    this.Type = TypeReferenceApiView.TokenType.ClassType;
                    this.IsNavigable = true;
                    break;
                case SymbolDisplayPartKind.EnumName:
                    this.Type = TypeReferenceApiView.TokenType.EnumType;
                    this.IsNavigable = true;
                    break;
                case SymbolDisplayPartKind.Punctuation:
                case SymbolDisplayPartKind.Space:
                    this.Type = TypeReferenceApiView.TokenType.Punctuation;
                    break;
                case SymbolDisplayPartKind.Keyword:
                    this.Type = TypeReferenceApiView.TokenType.BuiltInType;
                    break;
                default:
                    this.Type = TypeReferenceApiView.TokenType.TypeArgument;
                    break;
            }
        }
        
        public TokenApiView(string displayString, TypeReferenceApiView.TokenType type)
        {
            this.DisplayString = displayString;
            this.Type = type;
        }
    }
}
