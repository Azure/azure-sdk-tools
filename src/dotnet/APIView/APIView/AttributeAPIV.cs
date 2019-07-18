using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Text;

namespace APIView
{
    /// <summary>
    /// Class representing a C# attribute. Each attribute has a type and a 
    /// possible set of constructor arguments.
    /// </summary>
    public class AttributeAPIV
    {
        public TypeReferenceAPIV Type { get; set; }
        public AttributeConstructArgAPIV[] ConstructorArgs { get; set; }

        public AttributeAPIV() { }

        public AttributeAPIV(AttributeData attributeData)
        {
            this.Type = new TypeReferenceAPIV(attributeData.AttributeClass);

            var args = new List<AttributeConstructArgAPIV>();

            foreach (var arg in attributeData.ConstructorArguments)
            {
                args.Add(new AttributeConstructArgAPIV(arg));
            }
            foreach (var arg in attributeData.NamedArguments)
            {
                args.Add(new AttributeConstructArgAPIV(arg.Key, arg.Value));
            }
            this.ConstructorArgs = args.ToArray();
        }

        public override string ToString()
        {
            var returnString = new StringBuilder();
            var renderer = new TextRendererAPIV();
            renderer.Render(this, returnString);
            return returnString.ToString();
        }
    }
}
