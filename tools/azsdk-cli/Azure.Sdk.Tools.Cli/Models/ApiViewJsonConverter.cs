// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using System.Text.Json;

namespace Azure.Sdk.Tools.Cli.Models
{
    public class ApiChangeJsonConverter : JsonConverter<ApiChange>
    {
        public override ApiChange Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;

            var apiChange = new ApiChange();

            // Map changeType to Kind
            if (root.TryGetProperty("changeType", out var changeTypeElement))
            {
                apiChange.Kind = changeTypeElement.GetString() ?? string.Empty;
            }

            // Extract before/after for Detail
            string? before = null, after = null;
            if (root.TryGetProperty("before", out var beforeElement))
            {
                before = beforeElement.GetString();
            }
            if (root.TryGetProperty("after", out var afterElement))
            {
                after = afterElement.GetString();
            }

            // Build Detail from before/after
            if (!string.IsNullOrEmpty(before) && !string.IsNullOrEmpty(after))
            {
                apiChange.Detail = $"{before} -> {after}";
            }
            else
            {
                apiChange.Detail = before ?? after ?? string.Empty;
            }

            // Extract Symbol from meta
            if (root.TryGetProperty("meta", out var metaElement))
            {
                // Try to get the best symbol name
                string? symbolFromMeta = null;
                if (metaElement.TryGetProperty("methodName", out var methodNameElement))
                {
                    symbolFromMeta = methodNameElement.GetString();
                }
                else if (metaElement.TryGetProperty("fieldName", out var fieldNameElement))
                {
                    symbolFromMeta = fieldNameElement.GetString();
                }
                else if (metaElement.TryGetProperty("fqn", out var fqnElement))
                {
                    symbolFromMeta = fqnElement.GetString();
                }

                apiChange.Symbol = symbolFromMeta ?? string.Empty;

                // Flatten all meta properties into Metadata dictionary
                foreach (var property in metaElement.EnumerateObject())
                {
                    var value = property.Value.ValueKind switch
                    {
                        JsonValueKind.String => property.Value.GetString(),
                        JsonValueKind.True => "true",
                        JsonValueKind.False => "false",
                        JsonValueKind.Number => property.Value.GetRawText(),
                        JsonValueKind.Array => string.Join(",", property.Value.EnumerateArray().Select(e => e.GetString())),
                        _ => property.Value.GetRawText()
                    };

                    if (!string.IsNullOrEmpty(value))
                    {
                        apiChange.Metadata[property.Name] = value;
                    }
                }
            }

            // Add category to metadata if present
            if (root.TryGetProperty("category", out var categoryElement))
            {
                var category = categoryElement.GetString();
                if (!string.IsNullOrEmpty(category))
                {
                    apiChange.Metadata["category"] = category;
                }
            }

            return apiChange;
        }

        public override void Write(Utf8JsonWriter writer, ApiChange value, JsonSerializerOptions options)
        {
            throw new NotImplementedException("Writing ApiChange to JSON is not supported");
        }
    }
}
