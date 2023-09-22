using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using SwaggerApiParser.SwaggerApiView;

namespace SwaggerApiParser.Specs
{

    public class Schema : Items, ITokenSerializable
    {
        // Properties in Swagger Spec
        public string title { get; set; }
        public string description { get; set; }
        public int? maxProperties { get; set; }
        public int? minProperties { get; set; }
        public List<string> required { get; set; }
        public new Schema items { get; set; } // Should this be an array?
        public List<Schema> allOf { get; set; }
        public Dictionary<string, Schema> properties { get; set; }
        public JsonElement additionalProperties { get; set; }
        public string discriminator { get; set; }
        public bool readOnly { get; set; }
        public XML xml { get; set; }
        public ExternalDocs externalDocs { get; set; }
        public JsonElement example { get; set; }

        // Extra Properties For Parsing

        private List<SchemaTableItem> tableItems;
        private Queue<(Schema, SerializeContext)> propertyQueue = new();
        public string originalRef { get; set; }
        public Dictionary<string, Schema> allOfProperities { get; set; }

        public new CodeFileToken[] TokenSerialize(SerializeContext context)
        {
            string[] columns = new[] { "Model", "Field", "Type/Format", "Keywords", "Description" };
            tableItems = this.TokenSerializePropertyIntoTableItems(context, this.tableItems);
            var tableRet = new List<CodeFileToken>();
            var tableRows = new List<CodeFileToken>();
            foreach (var tableItem in this.tableItems)
            {
                tableRows.AddRange(tableItem.TokenSerialize(context));
            }
            if (tableRows.Count > 0)
            {
                tableRet.AddRange(TokenSerializer.TokenSerializeAsTableFormat(this.tableItems.Count, columns.Length, columns, tableRows.ToArray(), context.IteratorPath.CurrentNextPath("table")));
            }
            tableRet.Add(TokenSerializer.NewLine());
            return tableRet.ToArray();
        }

        public bool IsPropertyRequired(string propertyName)
        {
            return this.required != null && this.required.Contains(propertyName);
        }

        public new List<string> GetKeywords()
        {
            List<string> keywords = new List<string>();
            if (this.readOnly)
                keywords.Add("readOnly");

            if (this.maxProperties != null)
                keywords.Add($"maxProperties : {this.maxProperties}");

            if (this.minProperties != null)
                keywords.Add($"minProperties : {this.minProperties}");

            if (!string.IsNullOrEmpty(this.discriminator))
                keywords.Add($"discriminator : {this.discriminator}");

            if (!string.IsNullOrEmpty(this.discriminator))
                keywords.Add($"discriminator : {this.discriminator}");

            keywords.AddRange(base.GetKeywords());

            return keywords;
        }

        public string GetTypeFormat()
        {
            var typeFormat = this.format != null ? $"/{this.format}" : "";

            if (this.type is "array" && this.items is not null)
            {
                var reference = this.items.originalRef ?? this.items.@ref;
                var arrayType = Utils.GetDefinitionType(reference) ?? this.items.type;
                return this.type + $"<{arrayType}>";
            }

            if (this.originalRef != null)
            {
                return Utils.GetDefinitionType(this.originalRef) + typeFormat;
            }

            return this.type + typeFormat;
        }

        public List<SchemaTableItem> TokenSerializePropertyIntoTableItems(SerializeContext context, List<SchemaTableItem> retTableItems, bool serializeRef = true, string[] columns = null)
        {
            if (retTableItems == null)
            {
                retTableItems = new List<SchemaTableItem>();
                this.TokenSerializeInternal(context, this, retTableItems, serializeRef);
            }
            return retTableItems;
        }

        private CodeFileToken[] TokenSerializeInternal(SerializeContext context, Schema schema,
            List<SchemaTableItem> flattenedTableItems, bool serializeRef = true)
        {
            List<CodeFileToken> ret = new List<CodeFileToken>();
            if (serializeRef)
            {
                ret.Add(new CodeFileToken(Utils.GetDefinitionType(schema.originalRef), CodeFileTokenKind.TypeName));
                flattenedTableItems.Add(new SchemaTableItem() { Model = Utils.GetDefinitionType(schema.originalRef), TypeFormat = schema.type, Description = schema.description });
                ret.Add(TokenSerializer.NewLine());
                context.indent++;
            }


            if (schema.properties?.Count != 0)
            {
                // BUGBUG: Herein lies the problem. We're recursing down into child objects when we should be queuing them instead.
                TokenSerializeProperties(context, schema, schema.properties, ret, flattenedTableItems, serializeRef);
            }

            if (schema.allOfProperities?.Count != 0 && schema.allOf != null)
            {
                ret.Add(new CodeFileToken("allOf", CodeFileTokenKind.Keyword));
                ret.Add(TokenSerializer.Colon());
                ret.Add(TokenSerializer.NewLine());
                foreach (var allOfSchema in schema.allOf)
                {
                    if (allOfSchema != null)
                    {
                        ret.Add(new CodeFileToken(Utils.GetDefinitionType(allOfSchema.@ref), CodeFileTokenKind.TypeName));
                        ret.Add(TokenSerializer.NewLine());
                    }
                }

                TokenSerializeProperties(new SerializeContext(context.indent + 2, context.IteratorPath), schema, schema.allOfProperities, ret, flattenedTableItems, serializeRef);
            }

            if (schema.type == "array" && schema.items is not null)
            {
                SchemaTableItem arrayItem = new SchemaTableItem { Description = schema.description };
                arrayItem.TypeFormat = schema.items.type != null ? $"array<{schema.items.type}>" : $"array<{Utils.GetDefinitionType(schema.items.originalRef)}>";
                flattenedTableItems.Add(arrayItem);
                TokenSerializeArray(context, ret, schema, flattenedTableItems, serializeRef);
            }

            if (schema.type == "string")
            {
                if (schema.@enum != null)
                {
                    SchemaTableItem enumItem = new SchemaTableItem { Description = schema.description };
                    const string enumType = "enum<string>";
                    enumItem.TypeFormat = enumType;
                    if (schema.patternedObjects != null && schema.patternedObjects.ContainsKey("x-ms-enum"))
                    {
                        enumItem.Keywords = string.Join(", ", schema.GetKeywords());
                    }
                    flattenedTableItems.Add(enumItem);
                }
            }

            // Now recurse into nested model definitions so all properties are grouped with their models.
            //while (this.propertyQueue.TryDequeue(out var property))
            //{
            //    var (item, childContext) = property;
            //    ret.AddRange(item.TokenSerializeInternal(childContext, item, flattenedTableItems, serializeRef));
            //}

            return ret.ToArray();
        }

        private void TokenSerializeProperties(SerializeContext context, Schema schema, Dictionary<string, Schema> properties,
            List<CodeFileToken> ret, List<SchemaTableItem> flattenedTableItems, bool serializeRef = true)
        {
            if (properties == null)
            {
                return;
            }

            foreach (var (key, value) in properties)
            {
                ret.Add(new CodeFileToken(key, CodeFileTokenKind.Literal));
                ret.Add(TokenSerializer.Colon());
                if (value == null)
                {
                    continue;
                }

                // Normal case: If properties has values. Serialize each key value pair in properties.
                if ((value.properties != null && value.properties?.Count != 0))
                {
                    var keywords = GetPropertyKeywordsFromBaseSchema(schema, key, value);
                    SchemaTableItem item = new SchemaTableItem { Field = key, Description = value.description, Keywords = string.Join(", ", keywords), TypeFormat = value.GetTypeFormat() };
                    flattenedTableItems.Add(item);
                    ret.Add(TokenSerializer.NewLine());
                    if (serializeRef)
                    {
                        this.propertyQueue.Enqueue((value, new SerializeContext(context.indent + 1, context.IteratorPath)));
                    }
                }
                // Circular reference case: the ref won't be expanded. 
                else if (value.@ref != null)
                {
                    ret.Add(TokenSerializer.NewLine());
                    ret.Add(new CodeFileToken("<", CodeFileTokenKind.Punctuation));
                    var refName = value.@ref;
                    ret.Add(new CodeFileToken(refName.Split("/").Last(), CodeFileTokenKind.TypeName));
                    ret.Add(new CodeFileToken(">", CodeFileTokenKind.Punctuation));
                }
                // Array case: Serialize array.
                else if (value.type == "array")
                {
                    SchemaTableItem arrayItem = new SchemaTableItem();
                    arrayItem.Field = key;
                    arrayItem.Description = value.description;
                    var arrayType = "array";
                    if (value.items != null)
                    {
                        arrayType = (value.items.originalRef == null && value.items.@ref == null)
                            ? $"array<{value.items.type}>"
                            : $"array<{Utils.GetDefinitionType(value.items.originalRef ?? Utils.GetDefinitionType(value.items.@ref))}>";
                    }

                    arrayItem.TypeFormat = arrayType;
                    var keywords = GetPropertyKeywordsFromBaseSchema(schema, key, value);
                    arrayItem.Keywords = string.Join(", ", keywords);
                    flattenedTableItems.Add(arrayItem);
                    TokenSerializeArray(context, ret, value, flattenedTableItems, serializeRef);
                }
                else
                {
                    var keywords = GetPropertyKeywordsFromBaseSchema(schema, key, value);
                    SchemaTableItem item = new SchemaTableItem { Field = key, Description = value.description, TypeFormat = value.GetTypeFormat(), Keywords = string.Join(", ", keywords) };
                    flattenedTableItems.Add(item);
                    ret.Add(new CodeFileToken(value.type, CodeFileTokenKind.Keyword));
                    ret.Add(TokenSerializer.NewLine());
                }
            }
        }

        private void TokenSerializeArray(SerializeContext context, List<CodeFileToken> ret, Schema arraySchema, List<SchemaTableItem> flattenedTableItems, bool serializeRef)
        {
            ret.Add(new CodeFileToken("array", CodeFileTokenKind.Keyword));
            if (arraySchema.items == null)
            {
                return;
            }

            if (arraySchema.items.type != null && arraySchema.items.type != "object")
            {
                ret.Add(new CodeFileToken("<", CodeFileTokenKind.Punctuation));
                ret.Add(new CodeFileToken(arraySchema.items.type, CodeFileTokenKind.TypeName));
                ret.Add(new CodeFileToken(">", CodeFileTokenKind.Punctuation));
                ret.Add(TokenSerializer.NewLine());
            }
            else
            {
                ret.Add(new CodeFileToken("<", CodeFileTokenKind.Punctuation));
                var refName = arraySchema.items.originalRef ?? arraySchema.items.@ref ?? "";
                ret.Add(new CodeFileToken(refName.Split("/").Last(), CodeFileTokenKind.TypeName));
                ret.Add(new CodeFileToken(">", CodeFileTokenKind.Punctuation));
                ret.Add(TokenSerializer.NewLine());

                // circular reference
                if (arraySchema.items.@ref != null)
                {
                    return;
                }

                if (serializeRef)
                {
                    this.propertyQueue.Enqueue((arraySchema.items, new SerializeContext(context.indent + 1, context.IteratorPath)));
                }
            }
        }

        private static List<string> GetPropertyKeywordsFromBaseSchema(Schema baseSchema, string propertyName, Schema schema)
        {
            var keywords = new HashSet<string>();
            if (baseSchema.IsPropertyRequired(propertyName))
            {
                keywords.Add("required");
            }

            foreach (var kw in schema.GetKeywords())
            {
                keywords.Add(kw);
            }

            return keywords.ToList();
        }
    }
}
