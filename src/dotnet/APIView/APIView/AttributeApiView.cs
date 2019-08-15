using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace ApiView
{
    /// <summary>
    /// Class representing a C# attribute. Each attribute has a type and a 
    /// possible set of constructor arguments.
    /// </summary>
    public class AttributeApiView
    {
        public string Id { get; set; }
        public TypeReferenceApiView Type { get; set; }
        public AttributeConstructArgApiView[] ConstructorArgs { get; set; }

        public AttributeApiView() { }

        public AttributeApiView(AttributeData attributeData, string id)
        {
            this.Id = id;
            this.Type = new TypeReferenceApiView(attributeData.AttributeClass);

            var args = new List<AttributeConstructArgApiView>();

            foreach (var arg in attributeData.ConstructorArguments)
            {
                args.Add(new AttributeConstructArgApiView(arg));
            }
            foreach (var arg in attributeData.NamedArguments)
            {
                args.Add(new AttributeConstructArgApiView(arg.Key, arg.Value));
            }
            this.ConstructorArgs = args.ToArray();
        }

        public override string ToString()
        {
            var renderer = new TextRendererApiView();
            var list = new StringListApiView();
            renderer.Render(this, list);
            return list.First().DisplayString;
        }
    }
}
