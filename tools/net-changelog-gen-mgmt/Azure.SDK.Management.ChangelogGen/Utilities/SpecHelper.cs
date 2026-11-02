// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Text.Json;

namespace Azure.SDK.ChangelogGen.Utilities
{
    public static class SpecHelper
    {
        public static void ValidateTypeSpecInput(string typeSpecLocationFile, string metadataFile)
        {
            if (!File.Exists(typeSpecLocationFile))
                throw new FileNotFoundException($"TypeSpec configuration was not found at '{typeSpecLocationFile}'.", typeSpecLocationFile);

            if (!File.Exists(metadataFile))
                throw new FileNotFoundException($"TypeSpec metadata was not found at '{metadataFile}'.", metadataFile);
        }

        public static IReadOnlyDictionary<string, string> GetApiVersions(string metadataContent)
        {
            using JsonDocument metadata = JsonDocument.Parse(metadataContent);
            if (!metadata.RootElement.TryGetProperty("apiVersions", out JsonElement apiVersions) || apiVersions.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("Invalid metadata.json: missing required object 'apiVersions'.");

            Dictionary<string, string> versions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (JsonProperty apiVersion in apiVersions.EnumerateObject())
            {
                if (apiVersion.Value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(apiVersion.Value.GetString()))
                    throw new InvalidDataException($"Invalid metadata.json: apiVersions entry '{apiVersion.Name}' must have a non-empty string value.");

                versions.Add(apiVersion.Name, apiVersion.Value.GetString()!);
            }

            if (versions.Count == 0)
                throw new InvalidDataException("Invalid metadata.json: 'apiVersions' must contain at least one entry.");

            return versions;
        }

    }
}
