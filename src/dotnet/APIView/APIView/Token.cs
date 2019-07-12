using Microsoft.CodeAnalysis;

namespace APIView
{
    public class Token
    {
        public string DisplayString { get; set; }
        public bool IsNavigable { get; set; }
        
        public string NavigationID { get; set; }
        public TypeReference.TokenType Type { get; set; }

        public Token()
        {
            this.DisplayString = "";
        }

        public Token(SymbolDisplayPart part)
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
                    this.Type = TypeReference.TokenType.ClassType;
                    this.IsNavigable = true;
                    break;
                case SymbolDisplayPartKind.EnumName:
                    this.Type = TypeReference.TokenType.EnumType;
                    this.IsNavigable = true;
                    break;
                case SymbolDisplayPartKind.Punctuation:
                case SymbolDisplayPartKind.Space:
                    this.Type = TypeReference.TokenType.Punctuation;
                    break;
                case SymbolDisplayPartKind.Keyword:
                    this.Type = TypeReference.TokenType.BuiltInType;
                    break;
                default:
                    this.Type = TypeReference.TokenType.TypeArgument;
                    break;
            }
        }
        
        public Token(string displayString, TypeReference.TokenType type)
        {
            this.DisplayString = displayString;
            this.Type = type;
        }
    }
}
