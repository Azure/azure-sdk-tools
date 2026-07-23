// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Services.Notification
{
    /// <summary>
    /// Serializes a list of email addresses as a single ';'-separated string (and deserializes it back).
    /// The notification service expects recipients as a concatenated string rather than a JSON array.
    /// </summary>
    public class SemicolonSeparatedStringConverter : JsonConverter<List<string>>
    {
        private const char Separator = ';';

        public override List<string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                return [];
            }

            return value
                .Split(Separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }

        public override void Write(Utf8JsonWriter writer, List<string> value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value is null ? string.Empty : string.Join(Separator, value));
        }
    }
}
