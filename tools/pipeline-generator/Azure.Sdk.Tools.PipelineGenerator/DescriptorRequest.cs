using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Microsoft.TeamFoundation.Build.WebApi;

namespace PipelineGenerator
{
    public class DescriptorRequest
    {
        [JsonPropertyName("path")]
        public string Path { get; set; }

        [JsonPropertyName("previousPath")]
        public string PreviousPath { get; set; }

        public static SdkComponent ToComponent(string path)
        {
            var normalized = path.Replace('\\', '/');
            var filename = System.IO.Path.GetFileName(normalized);
            var parts = System.IO.Path.GetFileNameWithoutExtension(filename).Split('.');
            return new SdkComponent
            {
                Name = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(normalized)),
                RelativeYamlPath = normalized,
                Variant = parts.Length == 1 ? null : parts[1]
            };
        }

        public static BuildDefinition ResolveDefinition(IEnumerable<BuildDefinition> definitions, string repository,
            string name, string previousName, string path, string previousPath)
        {
            var matches = definitions.Where(d =>
                string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase) ||
                (previousName != null && string.Equals(d.Name, previousName, StringComparison.OrdinalIgnoreCase)) ||
                (string.Equals(d.Repository?.Name, repository, StringComparison.OrdinalIgnoreCase) &&
                 d.Process is YamlProcess yaml &&
                 (Normalize(yaml.YamlFilename) == path || (previousPath != null && Normalize(yaml.YamlFilename) == previousPath))))
                .ToArray();
            if (matches.Length > 1 || (matches.Length == 1 &&
                matches[0].Name != name && matches[0].Name != previousName))
            {
                throw new InvalidOperationException($"Ambiguous definition identity for '{path}'. Reconcile duplicate or manually renamed definitions before retrying.");
            }
            return matches.SingleOrDefault();
        }

        private static string Normalize(string path) => path?.Replace('\\', '/').TrimStart('/');

        public static bool Relocate(BuildDefinition definition,
            string repository, string name, string path, string previousPath)
        {
            if (!string.Equals(definition.Repository?.Name, repository, StringComparison.OrdinalIgnoreCase) ||
                definition.Process is not YamlProcess yaml)
            {
                throw new InvalidOperationException($"Definition {definition.Id} does not identify a YAML pipeline in {repository}.");
            }

            var current = Normalize(yaml.YamlFilename);
            path = path.Replace('\\', '/');
            if (current != path && (previousPath == null || current != previousPath.Replace('\\', '/')))
            {
                throw new InvalidOperationException($"Ambiguous definition {definition.Id}: '{current}' is neither '{path}' nor its supplied previous path.");
            }

            var changed = definition.Name != name || current != path;
            definition.Name = name;
            yaml.YamlFilename = path;
            return changed;
        }
    }
}
