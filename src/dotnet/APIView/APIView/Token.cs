using Microsoft.CodeAnalysis;

namespace APIView
{
    public class Token
    {
        public string DisplayString { get; set; }
        public bool IsNavigable { get; set; }
        public bool IsString { get; set; }
        public string NavigationID { get; set; }
        public TypeReference.TypeName Type { get; set; }

        public Token()
        {
            this.DisplayString = "";
            this.IsNavigable = false;
            this.IsString = false;
            this.NavigationID = "";
            this.Type = TypeReference.TypeName.NullType;
        }

        public Token(TypedConstant part)
        {
            this.DisplayString = part.Value.ToString();
            this.IsNavigable = false;
            this.NavigationID = "";
            if (part.Type.Name.Equals("String"))
            {
                this.IsString = true;
                this.Type = TypeReference.TypeName.ValueType;
            }
            else
            {
                this.IsString = false;
                this.Type = TypeReference.TypeName.Punctuation;
            }
        }

        public Token(SymbolDisplayPart part)
        {
            this.DisplayString = part.ToString();
            this.IsNavigable = false;
            this.IsString = false;
            this.NavigationID = part.ToString();
            switch (part.Kind)
            {
                case SymbolDisplayPartKind.ClassName:
                    this.Type = TypeReference.TypeName.ClassType;
                    break;
                case SymbolDisplayPartKind.EnumName:
                    this.Type = TypeReference.TypeName.EnumType;
                    break;
                case SymbolDisplayPartKind.ErrorTypeName:
                    this.Type = TypeReference.TypeName.ClassType;
                    break;
                case SymbolDisplayPartKind.InterfaceName:
                    this.Type = TypeReference.TypeName.ClassType;
                    break;
                case SymbolDisplayPartKind.Punctuation:
                    this.Type = TypeReference.TypeName.Punctuation;
                    break;
                case SymbolDisplayPartKind.Space:
                    this.Type = TypeReference.TypeName.Punctuation;
                    break;
                case SymbolDisplayPartKind.Keyword:
                    if (part.ToString() == "string")
                        this.IsString = true;
                    this.Type = TypeReference.TypeName.BuiltInType;
                    break;
                default:
                    this.Type = TypeReference.TypeName.SpecialType;
                    this.IsNavigable = true;
                    break;
            }
        }
        
        public Token(string displayString, TypeReference.TypeName type)
        {
            this.DisplayString = displayString;
            this.Type = type;
        }
    }
}
