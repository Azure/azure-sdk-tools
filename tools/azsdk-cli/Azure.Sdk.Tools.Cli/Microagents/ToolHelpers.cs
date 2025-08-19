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
    /// Try to resolve a provided path against a base directory, ensuring the result stays within the base directory.
    /// </summary>
    /// <param name="baseDirectory">The base directory that bounds all operations.</param>
    /// <param name="relativePath">A relative path provided by the user or caller.</param>
    /// <param name="fullPath">Outputs the resolved full path if successful; otherwise it's null.</param>
    /// <returns>True if the path resolves within the base directory; otherwise false.</returns>
    public static bool TryGetSafeFullPath(string baseDirectory, string relativePath, out string fullPath)
    {
        fullPath = default;

        try
        {
            if (string.IsNullOrWhiteSpace(baseDirectory))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return false;
            }

            if (Path.IsPathRooted(relativePath))
            {
                return false;
            }

            var baseFullPath = Path.GetFullPath(baseDirectory);
            var combinedFullPath = Path.GetFullPath(Path.Join(baseFullPath, relativePath));

            var baseWithSep = baseFullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

            if (!combinedFullPath.StartsWith(baseWithSep, StringComparison.Ordinal) &&
                !string.Equals(combinedFullPath, baseFullPath, StringComparison.Ordinal))
            {
                return false;
            }

            fullPath = combinedFullPath;
            return true;
        }
        catch
        {
            // Catch any path-related exceptions (e.g., invalid chars) and return false per Try* contract.
            return false;
        }
    }

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
