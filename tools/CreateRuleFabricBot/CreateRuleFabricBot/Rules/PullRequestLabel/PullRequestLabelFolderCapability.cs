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
        private static readonly string s_template = @"
{
      ""taskType"": ""trigger"",
      ""capabilityId"": ""PrAutoLabel"",
      ""subCapability"": ""Path"",
      ""version"": ""1.0"",
      ""id"": ""###taskId###"",
      ""config"": {
        ""configs"": [
            ###labelConfig###
        ],
      ""taskName"" :""Auto PR based on folder paths ""
      }
    }
";

        private static readonly string s_configTemplate = @"
          {
            ""label"": ""###label###"",
            ""pathFilter"": [ ###srcFolders### ],
            ""exclude"": [ """" ]
          }
";
        // Note: By using an empty string in the exclude portion above, the rule we create will allow multiple labels (from different folders) to be applied to the same PR.

        public PullRequestLabelFolderCapability(string org, string name, string configurationFile)
            : base(org, name, configurationFile)
        {
        }

        private List<PathConfig> _entriesToCreate = new List<PathConfig>();

        public override string GetPayload()
        {
            StringBuilder configPayload = new StringBuilder();
            Colorizer.WriteLine("Found the following rules:");

            // Create the payload.
            foreach (var entry in _entriesToCreate)
            {
                // get the payload
                Colorizer.WriteLine("[Cyan!{0}] => [Magenta!{1}]", entry.Path, entry.Label);

                configPayload.Append(entry.ToString());
                configPayload.Append(',');
            }

            // remove the trailing ','
            configPayload.Remove(configPayload.Length - 1, 1);

            // create the payload from the template
            return s_template
                .Replace("###taskId###", GetTaskId())
                .Replace("###labelConfig###", configPayload.ToString());
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

                _entriesToCreate.Add(new PathConfig(entries[i].PathExpression, entries[i].PRLabels.First()));
            }
        }

        public override string GetTaskId()
        {
            return $"AzureSDKPullRequestLabelFolder_{_owner}_{_repo}";
        }

        private string ToConfigString(CodeOwnerEntry entry)
        {
            string result = s_configTemplate;

            result = result.Replace("###label###", entry.PRLabels.First());

            // at this point we should remove the leading '/' if any
            if (entry.PathExpression.StartsWith("/"))
            {
                entry.PathExpression = entry.PathExpression.Substring(1);
            }
            result = result.Replace("###srcFolders###", $"\"{entry.PathExpression}\"");

            return result;
        }
    }
}
