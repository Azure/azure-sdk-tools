using Microsoft.CodeAnalysis;

namespace ApiView
{
    public class AttributeConstructArgApiv
    {
        public bool IsNamed => Name != null;
        public string Name { get; set; }
        public string Value { get; set; }

        public AttributeConstructArgApiv() { }

        public AttributeConstructArgApiv(TypedConstant value)
        {
            if (value.Type.SpecialType == SpecialType.System_String)
                this.Value = "\"" + value.Value.ToString() + "\"";
            else
                this.Value = value.Value.ToString();
        }

        public AttributeConstructArgApiv(string name, TypedConstant value)
        {
            this.Name = name;
            if (value.Type.SpecialType == SpecialType.System_String)
                this.Value = "\"" + value.Value.ToString() + "\"";
            else
                this.Value = value.Value.ToString();
        }
    }
}
