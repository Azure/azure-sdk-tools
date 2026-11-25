using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using PipelineGenerator.CommandParserOptions;

namespace PipelineGenerator
{
    public static class PostMergeRequest
    {
        public static DescriptorRequest[] Validate(PreparePostMergeOptions options)
        {
            if (!new[] { "java", "js", "python", "net", "go" }
                .Select(language => $"azure-sdk-for-{language}").Contains(options.RepositoryName, StringComparer.Ordinal))
            {
                throw new ArgumentException($"Unsupported SDK repository '{options.RepositoryName}'.");
            }
            if (options.MergeCommitSha == null || !Regex.IsMatch(options.MergeCommitSha, @"\A[0-9a-f]{40}\z"))
            {
                throw new ArgumentException("MergeCommitSha must be a full lowercase Git SHA.");
            }
            using var json = JsonDocument.Parse(options.DescriptorPaths);
            if (json.RootElement.ValueKind != JsonValueKind.Array ||
                json.RootElement.GetArrayLength() is < 1 or > 100)
            {
                throw new ArgumentException("DescriptorPaths must contain between 1 and 100 descriptors.");
            }
            foreach (var descriptor in json.RootElement.EnumerateArray())
            {
                if (descriptor.ValueKind != JsonValueKind.Object)
                {
                    throw new ArgumentException("Each descriptor must be an object.");
                }
                var keys = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in descriptor.EnumerateObject())
                {
                    if (!keys.Add(property.Name) || property.Name is not ("path" or "previousPath") ||
                        property.Value.ValueKind != JsonValueKind.String)
                    {
                        throw new ArgumentException("Descriptors require path and optional previousPath strings only.");
                    }
                    var path = property.Value.GetString();
                    if (!Regex.IsMatch(path, @"\Asdk/(?:[\p{L}\p{N}_.-]+/)+ci(?:\.\w+)?\.yml\z") ||
                        path.Split('/').Any(segment => segment is "." or ".."))
                    {
                        throw new ArgumentException($"Unsupported descriptor path '{path}'.");
                    }
                }
                if (!keys.Contains("path"))
                {
                    throw new ArgumentException("Each descriptor requires path.");
                }
            }
            var requests = JsonSerializer.Deserialize<DescriptorRequest[]>(options.DescriptorPaths);
            var paths = new HashSet<string>(StringComparer.Ordinal);
            var previousPaths = new HashSet<string>(StringComparer.Ordinal);
            var identities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var request in requests)
            {
                if (!paths.Add(request.Path))
                {
                    throw new ArgumentException($"Duplicate descriptor '{request.Path}'.");
                }
                var component = DescriptorRequest.ToComponent(request.Path);
                if (!identities.Add($"{component.Name}|{component.Variant}"))
                {
                    throw new ArgumentException("Descriptor targets collide under the pipeline naming convention.");
                }
                if (request.PreviousPath != null &&
                    (request.PreviousPath == request.Path || !previousPaths.Add(request.PreviousPath)))
                {
                    throw new ArgumentException("Rename mappings must be distinct and one-to-one.");
                }
            }
            if (paths.Overlaps(previousPaths))
            {
                throw new ArgumentException("Rename chains and swaps require manual identity reconciliation.");
            }
            return requests;
        }

        public static void ValidateTrustedEnvironment(Func<string, string> environment)
        {
            if (environment("BUILD_REPOSITORY_NAME") != PostMergeConstants.ToolsRepository ||
                environment("BUILD_SOURCEBRANCH") != PostMergeConstants.ToolsBranch ||
                environment("SYSTEM_TEAMPROJECT") != PostMergeConstants.Project ||
                environment("SYSTEM_COLLECTIONURI") != PostMergeConstants.CollectionUri)
            {
                throw new InvalidOperationException("Preparation must run from the tools default branch in azure-sdk/internal.");
            }
        }

        public static string[] GetDirectories(IEnumerable<DescriptorRequest> requests)
        {
            var directories = requests.Select(r => r.Path[..r.Path.LastIndexOf('/')])
                .Distinct(StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal).ToArray();
            return directories.Where(candidate =>
                !directories.Any(parent => candidate.StartsWith(parent + "/", StringComparison.Ordinal))).ToArray();
        }
    }
}
