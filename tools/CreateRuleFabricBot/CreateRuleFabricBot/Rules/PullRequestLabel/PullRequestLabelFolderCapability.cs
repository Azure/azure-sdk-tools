using Azure.Sdk.Tools.CodeOwnersParser;
using Newtonsoft.Json.Linq;
using OutputColorizer;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Encodings.Web;

namespace CreateRuleFabricBot.Rules.PullRequestLabel
{
    public class PullRequestLabelFolderCapability : BaseCapability
    {
        private List<PathConfig> _entriesToCreate = new List<PathConfig>();

        public PullRequestLabelFolderCapability(string org, string name, string configurationFile)
            : base(org, name, configurationFile)
        {
        }

        public override string GetPayload()
        {
            // Display the payload on the screen
            foreach (var entry in _entriesToCreate)
            {
                Colorizer.WriteLine("[Cyan!{0}] => [Magenta!{1}]", entry.Path, entry.Label);
            }

            // create the payload from the template
            JObject payload = new JObject(
                new JProperty("taskType", "trigger"),
                new JProperty("capabilityId", "PrAutoLabel"),
                new JProperty("subCapability", "Path"),
                new JProperty("version", "1.0"),
                new JProperty("id", new JValue(GetTaskId())),
                new JProperty("config",
                    new JObject(
                        new JProperty("configs",new JArray(_entriesToCreate.Select(tc => tc.GetJsonPayload()))),
                        new JProperty("taskName", "Auto PR based on folder paths ")
                    )
                ));

            return payload.ToString();
        }

        /// <summary>
        /// Add an entry for a folder with a specified label
        /// </summary>
        public void AddEntry(string pathExpression, string label)
        {
            _entriesToCreate.Add(new PathConfig(pathExpression, label));
        }

        internal override void ReadConfigurationFromFile(string configurationFile)
        {
            List<CodeOwnerEntry> entries = CodeOwnersFile.ParseFile(configurationFile);

            // Filter our the list of entries that we want to create.
            for (int i = 0; i < entries.Count; i++)
            {
                // Entries with wildcards are not yet supported
                if (entries[i].ContainsWildcard)
                {
                    // log a warning there

                    if (entries[i].PRLabels.Any())
                    {
                        Colorizer.WriteLine("[Yellow!Warning]: The path '[Cyan!{0}]' contains a wildcard and a label '[Magenta!{1}]' which is not supported!", entries[i].PathExpression, string.Join(',', entries[i].PRLabels));
                    }

                    continue; //TODO: regex expressions are not yet supported
                }

                if (entries[i].PathExpression.IndexOf(CodeOwnerEntry.MissingFolder, StringComparison.OrdinalIgnoreCase) != -1)
                {
                    Colorizer.WriteLine("[Yellow!Warning]: The path '[Cyan!{0}]' is marked with the non-existing path marker.", entries[i].PathExpression);

                    continue;
                }

                // Entries with more than one label are not yet supported.
                if (entries[i].PRLabels.Count > 1)
                {
                    Colorizer.WriteLine("[Yellow!Warning]: Multiple labels for the same path '[Cyan!{0}]' are not yet supported", entries[i].PathExpression);
                    continue;
                }

                if (entries[i].PRLabels.Count == 0)
                {
                    Colorizer.WriteLine("[Yellow!Warning]: The path '[Cyan!{0}]' does not contain a label.", entries[i].PathExpression, string.Join(',', entries[i].PRLabels));
                    continue;
                }

                AddEntry(entries[i].PathExpression, entries[i].PRLabels.First());
            }
        }

        public override string GetTaskId()
        {
            return $"AzureSDKPullRequestLabelFolder_{_repo}_{_owner}";
        }
    }
}
