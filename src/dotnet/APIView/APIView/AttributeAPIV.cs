using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace ApiView
{
    /// <summary>
    /// Class representing a C# attribute. Each attribute has a type and a 
    /// possible set of constructor arguments.
    /// </summary>
    public class AttributeApiv
    {
        public string Id { get; set; }
        public TypeReferenceApiv Type { get; set; }
        public AttributeConstructArgApiv[] ConstructorArgs { get; set; }

        public AttributeApiv() { }

        public AttributeApiv(AttributeData attributeData, string id)
        {
            this.Id = id;
            this.Type = new TypeReferenceApiv(attributeData.AttributeClass);

            var args = new List<AttributeConstructArgApiv>();

            foreach (var arg in attributeData.ConstructorArguments)
            {
                args.Add(new AttributeConstructArgApiv(arg));
            }
            foreach (var arg in attributeData.NamedArguments)
            {
                args.Add(new AttributeConstructArgApiv(arg.Key, arg.Value));
            }
            this.ConstructorArgs = args.ToArray();
        }

        public override string ToString()
        {
            var renderer = new TextRendererApiv();
            var list = new StringListApiv();
            renderer.Render(this, list);
            return list.First().DisplayString;
        }
    }
}
