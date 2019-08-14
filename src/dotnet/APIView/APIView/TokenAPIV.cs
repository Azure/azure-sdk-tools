using Microsoft.CodeAnalysis;

namespace ApiView
{
    public class TokenApiv
    {
        public string Id { get; set; }
        public string DisplayString { get; set; }
        public bool IsNavigable { get; set; }
        
        public TypeReferenceApiv.TokenType Type { get; set; }

        public TokenApiv()
        {
            this.DisplayString = "";
        }

        public TokenApiv(SymbolDisplayPart part)
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
                    this.Type = TypeReferenceApiv.TokenType.ClassType;
                    this.IsNavigable = true;
                    break;
                case SymbolDisplayPartKind.EnumName:
                    this.Type = TypeReferenceApiv.TokenType.EnumType;
                    this.IsNavigable = true;
                    break;
                case SymbolDisplayPartKind.Punctuation:
                case SymbolDisplayPartKind.Space:
                    this.Type = TypeReferenceApiv.TokenType.Punctuation;
                    break;
                case SymbolDisplayPartKind.Keyword:
                    this.Type = TypeReferenceApiv.TokenType.BuiltInType;
                    break;
                default:
                    this.Type = TypeReferenceApiv.TokenType.TypeArgument;
                    break;
            }
        }
        
        public TokenApiv(string displayString, TypeReferenceApiv.TokenType type)
        {
            this.DisplayString = displayString;
            this.Type = type;
        }
    }
}
