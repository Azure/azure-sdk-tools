using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using APIView;

namespace SwaggerApiParser
{
    public class Header : BaseSchema
    {
    }

    public class Definition : BaseSchema
    {
    }

    public class SchemaTableItem
    {
        public String Model;
        public String Field;
        public String TypeFormat;
        public Boolean Required;
        public String Description;

        public CodeFileToken[] TokenSerialize()
        {
            var requiredString = this.Required ? "true" : "";
            List<CodeFileToken> ret = new List<CodeFileToken>();
            ret.AddRange(TokenSerializer.TableCell(new[] {new CodeFileToken(this.Model, CodeFileTokenKind.MemberName)}));
            ret.AddRange(TokenSerializer.TableCell(new[] {new CodeFileToken(this.Field, CodeFileTokenKind.Literal)}));
            ret.AddRange(TokenSerializer.TableCell(new[] {new CodeFileToken(this.TypeFormat, CodeFileTokenKind.Keyword)}));
            ret.AddRange(TokenSerializer.TableCell(new[] {new CodeFileToken(requiredString, CodeFileTokenKind.Literal)}));
            ret.AddRange(TokenSerializer.TableCell(new[] {new CodeFileToken(this.Description, CodeFileTokenKind.Literal)}));
            return ret.ToArray();
        }
    }

    public class BaseSchema : ITokenSerializable
    {
        public string type { get; set; }
        public string description { get; set; }
        public string format { get; set; }
        public string originalRef { get; set; }

        public List<BaseSchema> allOf { get; set; }
        public List<BaseSchema> anyOf { get; set; }

        public List<BaseSchema> oneOf { get; set; }
        public BaseSchema items { get; set; }

        // public Boolean additionalProperties { get; set; }
        public bool readOnly { get; set; }
        public string discriminator { get; set; }
        public Dictionary<string, BaseSchema> properties { get; set; }
        public Dictionary<string, BaseSchema> allOfProperities { get; set; }

        [JsonPropertyName("x-ms-discriminator-value")]
        public string xMsDiscriminatorValue { get; set; }

        public List<string> required { get; set; }


        [JsonPropertyName("$ref")] public string Ref { get; set; }

        private List<SchemaTableItem> tableItems;


        public bool IsRefObj()
        {
            return this.Ref != null;
        }

        public string GetTypeFormat()
        {
            var typeFormat = this.format != null ? $"/{this.format}" : "";


            if (this.type is "array")
            {
                var reference = this.items.originalRef ?? this.items.Ref;
                var arrayType = Utils.GetDefinitionType(reference) ?? this.items.type;
                return this.type + $"<{arrayType}>";
            }

            if (this.originalRef != null)
            {
                return Utils.GetDefinitionType(this.originalRef) + typeFormat;
            }

            return this.type + typeFormat;
        }

        private CodeFileToken[] TokenSerializeInternal(SerializeContext context, BaseSchema schema, ref List<SchemaTableItem> flattenedTableItems)
        {
            List<CodeFileToken> ret = new List<CodeFileToken>();
            if (schema.originalRef != null)
            {
                // ret.Add(TokenSerializer.Intent(context.intent));
                ret.Add(new CodeFileToken(Utils.GetDefinitionType(schema.originalRef), CodeFileTokenKind.TypeName));
                flattenedTableItems.Add(new SchemaTableItem() {Model = Utils.GetDefinitionType(schema.originalRef), TypeFormat = this.GetTypeFormat(), Description = this.description});
                ret.Add(TokenSerializer.NewLine());
                context.intent++;
            }


            if (schema.properties?.Count != 0)
            {
                TokenSerializeProperties(context, schema, schema.properties, ret, ref flattenedTableItems);
            }

            if (schema.allOfProperities?.Count != 0 && schema.allOf != null)
            {
                // ret.Add(TokenSerializer.Intent(context.intent));
                ret.Add(new CodeFileToken("allOf", CodeFileTokenKind.Keyword));
                ret.Add(TokenSerializer.Colon());
                ret.Add(TokenSerializer.NewLine());
                foreach (var allOfSchema in schema.allOf)
                {
                    if (allOfSchema != null)
                    {
                        // ret.Add(TokenSerializer.Intent(context.intent + 1));
                        ret.Add(new CodeFileToken(Utils.GetDefinitionType(allOfSchema.Ref), CodeFileTokenKind.TypeName));
                        ret.Add(TokenSerializer.NewLine());
                    }
                }

                TokenSerializeProperties(new SerializeContext(context.intent + 2, context.IteratorPath), schema, schema.allOfProperities, ret, ref flattenedTableItems);
            }

            if (schema.type == "array")
            {
                // ret.Add(TokenSerializer.Intent(context.intent));
                TokenSerializeArray(context, ret, schema, ref flattenedTableItems);
            }

            return ret.ToArray();
        }

        private static void TokenSerializeProperties(SerializeContext context, BaseSchema schema, Dictionary<string, BaseSchema> properties, List<CodeFileToken> ret, ref List<SchemaTableItem> flattenedTableItems)
        {
            if (properties == null)
            {
                return;
            }

            foreach (var kv in properties)
            {
                SchemaTableItem item = new SchemaTableItem {Field = kv.Key, Description = kv.Value.description, TypeFormat = kv.Value.GetTypeFormat()};
                flattenedTableItems.Add(item);

                // ret.Add(TokenSerializer.Intent(context.intent));
                ret.Add(new CodeFileToken(kv.Key, CodeFileTokenKind.Literal));
                ret.Add(TokenSerializer.Colon());

                // Normal case: If properties is has values. Serialize each key value pair in properties.
                if ((kv.Value.properties != null && kv.Value.properties?.Count != 0))
                {
                    ret.Add(TokenSerializer.NewLine());
                    ret.AddRange(schema.TokenSerializeInternal(new SerializeContext(context.intent + 1, context.IteratorPath), kv.Value, ref flattenedTableItems));
                }
                // Circular reference case: the ref won't be expanded. 
                else if (kv.Value.Ref != null)
                {
                    ret.Add(TokenSerializer.NewLine());
                    // ret.Add(TokenSerializer.Intent(context.intent + 1));
                    ret.Add(new CodeFileToken("<", CodeFileTokenKind.Punctuation));
                    var refName = kv.Value.Ref;
                    ret.Add(new CodeFileToken(refName.Split("/").Last(), CodeFileTokenKind.TypeName));
                    ret.Add(new CodeFileToken(">", CodeFileTokenKind.Punctuation));
                }
                // Array case: Serialize array.
                else if (kv.Value.type == "array")
                {
                    TokenSerializeArray(context, ret, kv.Value, ref flattenedTableItems);
                }
                else
                {
                    ret.Add(new CodeFileToken(kv.Value.type, CodeFileTokenKind.Keyword));
                    ret.Add(TokenSerializer.NewLine());
                }
            }
        }

        private static void TokenSerializeArray(SerializeContext context, List<CodeFileToken> ret, BaseSchema arraySchema, ref List<SchemaTableItem> flattenedTableItems)
        {
            ret.Add(new CodeFileToken("array", CodeFileTokenKind.Keyword));
            if (arraySchema.items.type != null)
            {
                ret.Add(new CodeFileToken("<", CodeFileTokenKind.Punctuation));
                ret.Add(new CodeFileToken(arraySchema.items.type, CodeFileTokenKind.TypeName));
                ret.Add(new CodeFileToken(">", CodeFileTokenKind.Punctuation));
                ret.Add(TokenSerializer.NewLine());
            }
            else
            {
                ret.Add(new CodeFileToken("<", CodeFileTokenKind.Punctuation));
                var refName = arraySchema.items.originalRef ?? arraySchema.items.Ref ?? "";
                ret.Add(new CodeFileToken(refName.Split("/").Last(), CodeFileTokenKind.TypeName));
                ret.Add(new CodeFileToken(">", CodeFileTokenKind.Punctuation));
                ret.Add(TokenSerializer.NewLine());
                ret.AddRange(arraySchema.items.TokenSerializeInternal(new SerializeContext(context.intent + 1, context.IteratorPath), arraySchema.items, ref flattenedTableItems));
            }
        }

        public CodeFileToken[] TokenSerialize(SerializeContext context)
        {
            if (this.tableItems == null)
            {
                this.tableItems = new List<SchemaTableItem>();
                this.TokenSerializeInternal(context, this, ref this.tableItems);
            }
            // var ret = this.TokenSerializeInternal(context, this, ref flattenedSchema);

            Console.WriteLine(this.tableItems);
            var tableRows = new List<CodeFileToken>();
            foreach (var tableItem in this.tableItems)
            {
                tableRows.AddRange(tableItem.TokenSerialize());
            }

            string[] columns = new[] {"Model", "Field", "Type/Format", "Required", "Description"};
            var tableRet = new List<CodeFileToken>();
            tableRet.AddRange(TokenSerializer.TokenSerializeAsTableFormat(this.tableItems.Count, 5, columns, tableRows.ToArray()));
            tableRet.Add(TokenSerializer.NewLine());
            return tableRet.ToArray();
        }
    }
}
