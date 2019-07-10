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
        public TypeReference Type { get; set; }
        public string[] ConstructorArgs { get; set; }  // TODO: Update to use TypeReference

        public AttributeAPIV() { }

        public AttributeAPIV(AttributeData attributeData)
        {
            this.Type = new TypeReference(attributeData);

            var args = new List<string>();
            foreach (var arg in attributeData.ConstructorArguments)
            {
                if (arg.Type.Name.Equals("String"))
                    args.Add("\"" + arg.Value.ToString() + "\"");
                else
                    args.Add(arg.Value.ToString());
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
