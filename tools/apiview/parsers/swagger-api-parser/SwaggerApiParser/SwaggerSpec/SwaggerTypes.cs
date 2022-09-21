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

    public class SchemaTableItem
    {
        public String Model { get; set; }
        public String Field { get; set; }
        public String TypeFormat { get; set; }

        public String Keywords { get; set; }
        public String Description { get; set; }


        public CodeFileToken[] TokenSerialize()
        {
            List<CodeFileToken> ret = new List<CodeFileToken>();
            string[] serializedFields = new[] {"Model", "Field", "TypeFormat", "Keywords", "Description"};
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
                    ret.AddRange(TokenSerializer.TableCell(new[] {new CodeFileToken(property.GetValue(this, null)?.ToString(), CodeFileTokenKind.Literal)}));
                }
            }

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
        
        public bool writeOnly { get; set; }
        
        public string discriminator { get; set; }
        
        [JsonPropertyName("x-ms-nullable")]
        public bool xMsNullable { get; set; }
        
        public Dictionary<string, BaseSchema> properties { get; set; }
        public Dictionary<string, BaseSchema> allOfProperities { get; set; }

        [JsonPropertyName("x-ms-discriminator-value")]
        public string xMsDiscriminatorValue { get; set; }

        public List<string> required { get; set; }

        public bool IsPropertyRequired(string propertyName)
        {
            return this.required != null && this.required.Contains(propertyName);
        }

        [JsonPropertyName("$ref")] public string Ref { get; set; }

        private List<SchemaTableItem> tableItems;


        public bool IsRefObj()
        {
            return this.Ref != null;
        }

        public List<String> GetKeywords()
        {
            List<string> keywords = new List<string>();
            if (this.readOnly)
            {
                keywords.Add("readOnly");
            }

            if (this.writeOnly)
            {
                keywords.Add("writeOnly");
            }

            if (this.xMsNullable)
            {
                keywords.Add("x-ms-nullable");
            }

            return keywords;
        }

        public String GetOriginRef()
        {
            return "";
            return this.originalRef ?? this.Ref;
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

        private CodeFileToken[] TokenSerializeInternal(SerializeContext context, BaseSchema schema, ref List<SchemaTableItem> flattenedTableItems, Boolean serializeRef = true)
        {
            List<CodeFileToken> ret = new List<CodeFileToken>();
            if (serializeRef || schema.GetOriginRef() != null)
            {
                // ret.Add(TokenSerializer.Intent(context.intent));
                ret.Add(new CodeFileToken(Utils.GetDefinitionType(schema.originalRef), CodeFileTokenKind.TypeName));
                flattenedTableItems.Add(new SchemaTableItem() {Model = Utils.GetDefinitionType(schema.originalRef), TypeFormat = schema.type, Description = schema.description});
                ret.Add(TokenSerializer.NewLine());
                context.intent++;
            }


            if (schema.properties?.Count != 0)
            {
                TokenSerializeProperties(context, schema, schema.properties, ret, ref flattenedTableItems, serializeRef);
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

                TokenSerializeProperties(new SerializeContext(context.intent + 2, context.IteratorPath), schema, schema.allOfProperities, ret, ref flattenedTableItems, serializeRef);
            }

            if (schema.type == "array")
            {
                // ret.Add(TokenSerializer.Intent(context.intent));

                SchemaTableItem arrayItem = new SchemaTableItem {Description = schema.description};
                var arrayType = schema.items.type != null ? $"array<{schema.items.type}>" : $"array<{Utils.GetDefinitionType(schema.items.originalRef)}>";
                arrayItem.TypeFormat = arrayType;
                flattenedTableItems.Add(arrayItem);
                if (serializeRef || schema.items.GetOriginRef() == null)
                {
                    TokenSerializeArray(context, ret, schema, ref flattenedTableItems, serializeRef);
                }
            }
            return ret.ToArray();
        }

        private static List<string> GetPropertyKeywordsFromBaseSchema(BaseSchema baseSchema, string propertyName, BaseSchema schema)
        {
            var keywords = new HashSet<string>();
            if (baseSchema.IsPropertyRequired(propertyName))
            {
                keywords.Add("required");
            }

            foreach (var it in schema.GetKeywords())
            {
                keywords.Add(it);
            }

            return keywords.ToList();
        }

        private static void TokenSerializeProperties(SerializeContext context, BaseSchema schema, Dictionary<string, BaseSchema> properties, List<CodeFileToken> ret, ref List<SchemaTableItem> flattenedTableItems,
            Boolean serializeRef = true)
        {
            if (properties == null)
            {
                return;
            }

            foreach (var kv in properties)
            {
                // ret.Add(TokenSerializer.Intent(context.intent));
                ret.Add(new CodeFileToken(kv.Key, CodeFileTokenKind.Literal));
                ret.Add(TokenSerializer.Colon());

                // Normal case: If properties is has values. Serialize each key value pair in properties.
                if ((kv.Value.properties != null && kv.Value.properties?.Count != 0))
                {
                    var keywords = GetPropertyKeywordsFromBaseSchema(schema, kv.Key, kv.Value);
                    SchemaTableItem item = new SchemaTableItem {Field = kv.Key, Description = kv.Value.description, Keywords = String.Join(",", keywords), TypeFormat = kv.Value.GetTypeFormat()};
                    flattenedTableItems.Add(item);
                    ret.Add(TokenSerializer.NewLine());
                    if (serializeRef || kv.Value.GetOriginRef() == null)
                    {
                        ret.AddRange(schema.TokenSerializeInternal(new SerializeContext(context.intent + 1, context.IteratorPath), kv.Value, ref flattenedTableItems, serializeRef));
                    }
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
                    SchemaTableItem arrayItem = new SchemaTableItem();
                    arrayItem.Field = kv.Key;
                    arrayItem.Description = kv.Value.description;
                    var arrayType = (kv.Value.items.originalRef == null && kv.Value.items.Ref == null)
                        ? $"array<{kv.Value.items.type}>"
                        : $"array<{Utils.GetDefinitionType(kv.Value.items.originalRef ?? Utils.GetDefinitionType(kv.Value.items.Ref))}>";
                    arrayItem.TypeFormat = arrayType;
                    var keywords = GetPropertyKeywordsFromBaseSchema(schema, kv.Key, kv.Value);
                    arrayItem.Keywords = string.Join(",", keywords);
                    flattenedTableItems.Add(arrayItem);
                    if (serializeRef || kv.Value.GetOriginRef() == null)
                    {
                        TokenSerializeArray(context, ret, kv.Value, ref flattenedTableItems, serializeRef);
                    }
                }
                else
                {
                    var keywords = GetPropertyKeywordsFromBaseSchema(schema, kv.Key, kv.Value);
                    SchemaTableItem item = new SchemaTableItem {Field = kv.Key, Description = kv.Value.description, TypeFormat = kv.Value.GetTypeFormat(), Keywords = string.Join(",", keywords)};
                    flattenedTableItems.Add(item);
                    ret.Add(new CodeFileToken(kv.Value.type, CodeFileTokenKind.Keyword));
                    ret.Add(TokenSerializer.NewLine());
                }
            }
        }

        private static void TokenSerializeArray(SerializeContext context, List<CodeFileToken> ret, BaseSchema arraySchema, ref List<SchemaTableItem> flattenedTableItems, Boolean serializeRef)
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
                ret.AddRange(arraySchema.items.TokenSerializeInternal(new SerializeContext(context.intent + 1, context.IteratorPath), arraySchema.items, ref flattenedTableItems, serializeRef));
            }
        }

        public void TokenSerializePropertyIntoTableItems(SerializeContext context, ref List<SchemaTableItem> retTableItems, Boolean serializeRef = true, string[] columns = null)
        {
            if (retTableItems == null)
            {
                retTableItems = new List<SchemaTableItem>();
                this.TokenSerializeInternal(context, this, ref retTableItems, serializeRef);
            }
            // var ret = this.TokenSerializeInternal(context, this, ref flattenedSchema);
        }

        public CodeFileToken[] TokenSerialize(SerializeContext context)
        {
            string[] columns = new[] {"Model", "Field", "Type/Format", "Keywords", "Description"};
            this.TokenSerializePropertyIntoTableItems(context, ref this.tableItems);
            var tableRet = new List<CodeFileToken>();

            var tableRows = new List<CodeFileToken>();
            foreach (var tableItem in this.tableItems)
            {
                tableRows.AddRange(tableItem.TokenSerialize());
            }

            tableRet.AddRange(TokenSerializer.TokenSerializeAsTableFormat(this.tableItems.Count, columns.Length, columns, tableRows.ToArray(), context.IteratorPath.CurrentNextPath("table")));
            tableRet.Add(TokenSerializer.NewLine());
            return tableRet.ToArray();
        }
    }
}
