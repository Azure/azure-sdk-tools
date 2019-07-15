using Microsoft.CodeAnalysis;

namespace APIView
{
    public class TokenAPIV
    {
        public string DisplayString { get; set; }
        public bool IsNavigable { get; set; }
        
        public string NavigationID { get; set; }
        public TypeReferenceAPIV.TokenType Type { get; set; }

        public TokenAPIV()
        {
            this.DisplayString = "";
        }

        public TokenAPIV(SymbolDisplayPart part)
        {
            this.DisplayString = part.ToString();
            this.IsNavigable = false;
            if (part.Symbol == null)
                this.NavigationID = "";
            else
            {
                var typeParamIndex = part.Symbol.ToDisplayString().LastIndexOf("<");
                if (typeParamIndex > 0)
                    this.NavigationID = part.Symbol.ToDisplayString().Remove(typeParamIndex);
                else
                    this.NavigationID = part.Symbol.ToDisplayString();
            }
            
            switch (part.Kind)
            {
                case SymbolDisplayPartKind.ClassName:
                case SymbolDisplayPartKind.ErrorTypeName:
                case SymbolDisplayPartKind.InterfaceName:
                case SymbolDisplayPartKind.StructName:
                    this.Type = TypeReferenceAPIV.TokenType.ClassType;
                    this.IsNavigable = true;
                    break;
                case SymbolDisplayPartKind.EnumName:
                    this.Type = TypeReferenceAPIV.TokenType.EnumType;
                    this.IsNavigable = true;
                    break;
                case SymbolDisplayPartKind.Punctuation:
                case SymbolDisplayPartKind.Space:
                    this.Type = TypeReferenceAPIV.TokenType.Punctuation;
                    break;
                case SymbolDisplayPartKind.Keyword:
                    this.Type = TypeReferenceAPIV.TokenType.BuiltInType;
                    break;
                default:
                    this.Type = TypeReferenceAPIV.TokenType.TypeArgument;
                    break;
            }
        }
        
        public TokenAPIV(string displayString, TypeReferenceAPIV.TokenType type)
        {
            this.DisplayString = displayString;
            this.Type = type;
        }
    }
}
