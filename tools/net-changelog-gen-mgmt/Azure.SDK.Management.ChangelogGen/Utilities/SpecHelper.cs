// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Markdig.Parsers;
using System;

namespace Azure.SDK.ChangelogGen.Utilities
{
    public static class SpecHelper
    {
        private static IEnumerable<string> GetTagsInBatch(Dictionary<string, object> config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            if (config.TryGetValue("batch", out object? value) && value is IEnumerable<object> batch)
            {
                foreach (var obj in batch)
                {
                    if (obj is Dictionary<object, object> dict)
                    {
                        string tag = GetTagInDefault(dict.ToDictionary(kv => kv.Key.ToString() ?? "", kv => kv.Value));
                        if (!string.IsNullOrEmpty(tag))
                            yield return tag;
                    }
                }
            }
        }

        private static string GetTagInDefault(Dictionary<string, object> config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            if (config.TryGetValue("tag", out object? value) && value is string tag)
                return tag;
            return "";
        }

        private static IEnumerable<string> GetTags(Dictionary<string, object> config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            string tag = GetTagInDefault(config);
            if (!string.IsNullOrEmpty(tag))
                yield return tag;
            foreach (string tagInBatch in GetTagsInBatch(config))
                yield return tagInBatch;
        }

        private static string LoadSpecRequired(string requirePath)
        {
            string require = requirePath.Replace("\\", "/");
            const string SPEC_PREFIX_BLOB = @"https://github.com/Azure/azure-rest-api-specs/blob/";
            const string SPEC_PREFIX_TREE = @"https://github.com/Azure/azure-rest-api-specs/tree/";
            const string SPEC_RAW_PREFIX = @"https://raw.githubusercontent.com/Azure/azure-rest-api-specs/";
            string webPath = "";
            if (require.StartsWith(SPEC_RAW_PREFIX, StringComparison.OrdinalIgnoreCase))
                webPath = require;
            else if (require.StartsWith(SPEC_PREFIX_BLOB, StringComparison.OrdinalIgnoreCase))
                webPath = string.Concat(SPEC_RAW_PREFIX, require.AsSpan(SPEC_PREFIX_BLOB.Length));
            else if (require.StartsWith(SPEC_PREFIX_TREE, StringComparison.OrdinalIgnoreCase))
                webPath = string.Concat(SPEC_RAW_PREFIX, require.AsSpan(SPEC_PREFIX_TREE.Length));

            string specReadme = "";
            if (!string.IsNullOrEmpty(webPath))
            {
                Logger.Log("Retrieve spec readme from " + webPath);
                using (HttpClient hc = new HttpClient())
                {
                    specReadme = hc.GetStringAsync(webPath).Result;
                }
            }
            else
            {
                if (File.Exists(require))
                {
                    Logger.Log("Retrieve spec readme from " + require);
                    specReadme = File.ReadAllText(require);
                }
                else
                {
                    Logger.Warning("Can't find spec readme from require : " + require);
                }
            }

            return specReadme;
        }

        public static List<string> GetSpecVersionTags(string autorestMdContent, out string specPath)
        {
            var autorestMd = MarkdownParser.Parse(autorestMdContent);
            var codeGenConfig = autorestMd.LoadYaml();
            string specReadme = "";
            List<string> tags;
            specPath = "";

            if (codeGenConfig.TryGetValue("require", out object? reqValue) && reqValue is string path)
            {
                specPath = path;
                specReadme = SpecHelper.LoadSpecRequired(specPath);
            }
            else
            {
                Logger.Warning("No require info found in autorest.md");
            }
     
            if (!string.IsNullOrEmpty(specReadme))
            {
                var specMd = MarkdownParser.Parse(specReadme);
                var specConfig = specMd.LoadYaml();
                tags = SpecHelper.GetTags(specConfig).ToList();
            }
            else
            {
                Logger.Log("No readme info found in Require in autorest.md. Try to parse tag from autorest.md directly");
                tags = SpecHelper.GetTags(codeGenConfig).ToList();
            }

            if (tags == null || tags.Count == 0)
                throw new InvalidOperationException("Failed to retrieve Tag from spec readme.md or autorest.md");

            return tags;
        }

        public static string GenerateGitHubPathToAutorestMd(string relativePathToAutorestMd)
        {
            return "https://github.com/Azure/azure-sdk-for-net/tree/main/" + relativePathToAutorestMd.Replace("\\", "/");
        }
    }
}
