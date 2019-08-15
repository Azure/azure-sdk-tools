using Microsoft.CodeAnalysis;

namespace ApiView
{
    public class AttributeConstructArgApiView
    {
        public bool IsNamed => Name != null;
        public string Name { get; set; }
        public string Value { get; set; }

        public AttributeConstructArgApiView() { }

        public AttributeConstructArgApiView(TypedConstant value)
        {
            if (value.Type.SpecialType == SpecialType.System_String)
                this.Value = "\"" + value.Value.ToString() + "\"";
            else
                this.Value = value.Value.ToString();
        }

        public AttributeConstructArgApiView(string name, TypedConstant value)
        {
            this.Name = name;
            if (value.Type.SpecialType == SpecialType.System_String)
                this.Value = "\"" + value.Value.ToString() + "\"";
            else
                this.Value = value.Value.ToString();
        }
    }
}
