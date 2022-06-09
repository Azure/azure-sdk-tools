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


        public bool IsRefObj()
        {
            return this.Ref != null;
        }

        private CodeFileToken[] TokenSerializeInternal(SerializeContext context, BaseSchema schema)
        {
            List<CodeFileToken> ret = new List<CodeFileToken>();
            if (schema.originalRef != null)
            {
                ret.Add(TokenSerializer.Intent(context.intent));
                ret.Add(new CodeFileToken(Utils.GetDefinitionType(schema.originalRef), CodeFileTokenKind.TypeName));
                ret.Add(TokenSerializer.NewLine());
                context.intent++;
            }


            if (schema.properties.Count != 0)
            {
                SerializeProperties(context, schema, schema.properties, ret);
            }

            if (schema.allOfProperities.Count != 0 && schema.allOf != null)
            {
                ret.Add(TokenSerializer.Intent(context.intent));
                ret.Add(new CodeFileToken("allOf", CodeFileTokenKind.Keyword));
                ret.Add(TokenSerializer.Colon());
                ret.Add(TokenSerializer.NewLine());
                foreach (var allOfSchema in schema.allOf)
                {
                    if (allOfSchema != null)
                    {
                        ret.Add(TokenSerializer.Intent(context.intent + 1));
                        ret.Add(new CodeFileToken(Utils.GetDefinitionType(allOfSchema.Ref), CodeFileTokenKind.TypeName));
                        ret.Add(TokenSerializer.NewLine());
                    }
                }

                SerializeProperties(new SerializeContext(context.intent + 2, context.IteratorPath), schema, schema.allOfProperities, ret);
            }

            return ret.ToArray();
        }

        private static void SerializeProperties(SerializeContext context, BaseSchema schema, Dictionary<string, BaseSchema> properties, List<CodeFileToken> ret)
        {
            foreach (var kv in properties)
            {
                ret.Add(TokenSerializer.Intent(context.intent));
                ret.Add(new CodeFileToken(kv.Key, CodeFileTokenKind.Literal));
                ret.Add(TokenSerializer.Colon());

                if (kv.Value.properties.Count != 0)
                {
                    ret.Add(TokenSerializer.NewLine());
                    ret.AddRange(schema.TokenSerializeInternal(new SerializeContext(context.intent + 1, context.IteratorPath), kv.Value));
                }
                else
                {
                    ret.Add(new CodeFileToken(kv.Value.type, CodeFileTokenKind.Keyword));
                    if (kv.Value.type == "array")
                    {
                        if (kv.Value.items.type != null)
                        {
                            ret.Add(new CodeFileToken("<", CodeFileTokenKind.Punctuation));
                            ret.Add(new CodeFileToken(kv.Value.items.type, CodeFileTokenKind.TypeName));
                            ret.Add(new CodeFileToken(">", CodeFileTokenKind.Punctuation));
                        }
                        else
                        {
                            ret.Add(new CodeFileToken("<", CodeFileTokenKind.Punctuation));
                            ret.Add(new CodeFileToken(kv.Value.items.Ref.Split("/").Last(), CodeFileTokenKind.TypeName));
                            ret.Add(new CodeFileToken(">", CodeFileTokenKind.Punctuation));
                        }
                    }

                    ret.Add(TokenSerializer.NewLine());
                }
            }
        }

        public CodeFileToken[] TokenSerialize(SerializeContext context)
        {
            return this.TokenSerializeInternal(context, this);
        }
    }
}
