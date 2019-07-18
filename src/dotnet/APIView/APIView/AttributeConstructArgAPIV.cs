using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace APIView
{
    public class AttributeConstructArgAPIV
    {
        public bool IsNamed => Name != null;
        public string Name { get; set; }
        public string Value { get; set; }

        public AttributeConstructArgAPIV() { }

        public AttributeConstructArgAPIV(TypedConstant value)
        {
            if (value.Type.SpecialType == SpecialType.System_String)
                this.Value = "\"" + value.Value.ToString() + "\"";
            else
                this.Value = value.Value.ToString();
        }

        public AttributeConstructArgAPIV(string name, TypedConstant value)
        {
            this.Name = name;
            if (value.Type.SpecialType == SpecialType.System_String)
                this.Value = "\"" + value.Value.ToString() + "\"";
            else
                this.Value = value.Value.ToString();
        }
    }
}
