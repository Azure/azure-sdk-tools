using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;

namespace Azure.Sdk.Tools.Cli.Microagents;

public static class ToolHelpers
{
    private static JsonSchemaExporterOptions exporterOptions = new()
    {
        TransformSchemaNode = (context, schema) =>
        {
            // Determine if a type or property and extract the relevant attribute provider.
            var attributeProvider = context.PropertyInfo is not null
                ? context.PropertyInfo.AttributeProvider
                : context.TypeInfo.Type;

            // Look up any description attributes.
            var descriptionAttr = attributeProvider?
                .GetCustomAttributes(inherit: true)
                .Select(attr => attr as DescriptionAttribute)
                .FirstOrDefault(attr => attr is not null);

            // Apply description attribute to the generated schema.
            if (descriptionAttr != null)
            {
                if (schema is not JsonObject jObj)
                {
                    // Handle the case where the schema is a Boolean.
                    JsonValueKind valueKind = schema.GetValueKind();
                    Debug.Assert(valueKind is JsonValueKind.True or JsonValueKind.False);
                    schema = jObj = new JsonObject();
                    if (valueKind is JsonValueKind.False)
                    {
                        jObj.Add("not", true);
                    }
                }

                jObj.Insert(0, "description", descriptionAttr.Description);
            }

            return schema;
        },
        TreatNullObliviousAsNonNullable = true,
    };

    /// <summary>
    /// Get a JSON schema representation of the given type for passing to an LLM.
    /// </summary>
    /// <param name="schema">The type to generate a schema for. Properties may be annotated with a Description attribute to provide a description to the LLM</param>
    /// <returns>The JSON schema as a string</returns>
    public static string GetJsonSchemaRepresentation(Type schema)
    {
        var node = JsonSerializerOptions.Default.GetJsonSchemaAsNode(schema, exporterOptions);
        return node.ToString();
    }
}
