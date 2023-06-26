using System;
using System.Collections.Generic;
using System.Linq;
using SwaggerApiParser.SwaggerApiView;

namespace SwaggerApiParser.Specs
{
    public class SchemaTableItem
    {
        public string Model { get; set; }
        public string Field { get; set; }
        public string TypeFormat { get; set; }
        public string Keywords { get; set; }
        public string Description { get; set; }

        public CodeFileToken[] TokenSerialize()
        {
            List<CodeFileToken> ret = new List<CodeFileToken>();
            string[] serializedFields = new[] { "Model", "Field", "TypeFormat", "Keywords", "Description" };
            ret.AddRange(this.TokenSerializeWithOptions(serializedFields));
            return ret.ToArray();
        }

        public CodeFileToken[] TokenSerializeWithOptions(string[] serializedFields)
        {
            List<CodeFileToken> ret = new List<CodeFileToken>();
            foreach (var property in this.GetType().GetProperties())
            {
                if (serializedFields.Contains(property.Name))
                {
                    ret.AddRange(TokenSerializer.TableCell(new[] { new CodeFileToken(property.GetValue(this, null)?.ToString(), CodeFileTokenKind.Literal) }));
                }
            }

            return ret.ToArray();
        }
    }
}
