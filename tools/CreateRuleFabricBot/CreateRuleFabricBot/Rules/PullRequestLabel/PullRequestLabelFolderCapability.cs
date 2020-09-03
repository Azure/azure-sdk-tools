using CreateRuleFabricBot.Rules.PullRequestLabel;
using OutputColorizer;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Encodings.Web;

namespace CreateRuleFabricBot.Rules.IssueRouting
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

        private readonly string _repo;
        private readonly string _owner;
        private readonly string _codeownersFile;

        public PullRequestLabelFolderCapability(string org, string name, string codeownersFile)
        {
            _repo = org;
            _owner = name;
            _codeownersFile = codeownersFile;
        }

        public override string GetPayload()
        {
            Colorizer.Write("Parsing CODEOWNERS table... ");
            List<CodeOwnerEntry> entries = ReadOwnersFromFile();
            Colorizer.WriteLine("[Green!Done]");

            StringBuilder configPayload = new StringBuilder();

            List<CodeOwnerEntry> entriesToCreate = new List<CodeOwnerEntry>();

            // Filter our the list of entries that we want to create.
            for (int i = 0; i < entries.Count; i++)
            {
                // Entries with wildcards are not yet supported
                if (entries[i].ContainsWildcard)
                {
                    // log a warning there

                    if (entries[i].Labels.Any())
                    {
                        Colorizer.WriteLine("[Yellow!Warning]: The path '[Cyan!{0}]' contains a wildcard and a label '[Magenta!{1}]' which is not supported!", entries[i].PathExpression, string.Join(',', entries[i].Labels));
                    }

                    continue; //TODO: regex expressions are not yet supported
                }

                // Entries with more than one label are not yet supported.
                if (entries[i].Labels.Count > 1)
                {
                    Colorizer.WriteLine("[Yellow!Warning]: Multiple labels for the same path '[Cyan!{0}]' are not yet supported", entries[i].PathExpression);
                    continue;
                }

                if (entries[i].Labels.Count == 0)
                {
                    Colorizer.WriteLine("[Yellow!Warning]: The path '[Cyan!{0}]' does not contain a label.", entries[i].PathExpression, string.Join(',', entries[i].Labels));
                    continue;
                }

                entriesToCreate.Add(entries[i]);
            }

            Colorizer.WriteLine("Found the following rules:");

            // Create the payload.
            foreach (var entry in entriesToCreate)
            {
                // get the payload
                string entryPayload = ToConfigString(entry);

                Colorizer.WriteLine("[Cyan!{0}] => [Magenta!{1}]", entry.PathExpression, entry.Labels.FirstOrDefault());

                configPayload.Append(ToConfigString(entry));
                configPayload.Append(',');
            }


            // remove the trailing ','
            configPayload.Remove(configPayload.Length - 1, 1);

            // Log the set of paths we are creating.


            // create the payload from the template
            return s_template
                .Replace("###taskId###", GetTaskId())
                .Replace("###labelConfig###", configPayload.ToString());
        }

        private string ToConfigString(CodeOwnerEntry entry)
        {
            string result = s_configTemplate;

            result = result.Replace("###label###", entry.Labels.First());

            // at this point we should remove the leading '/' if any
            if (entry.PathExpression.StartsWith("/"))
            {
                entry.PathExpression = entry.PathExpression.Substring(1);
            }
            result = result.Replace("###srcFolders###", $"\"{entry.PathExpression}\"");

            return result;
        }

        private string GetFileContents(string fileOrUri)
        {
            if (File.Exists(fileOrUri))
            {
                return File.ReadAllText(fileOrUri);
            }

            // try to parse it as an Uri
            Uri uri = new Uri(fileOrUri, UriKind.Absolute);
            if (uri.Scheme.ToLowerInvariant() != "https")
            {
                throw new ArgumentException("Cannot download off non-https uris");
            }

            // try to download it.
            using (HttpClient client = new HttpClient())
            {
                HttpResponseMessage response = client.GetAsync(fileOrUri).ConfigureAwait(false).GetAwaiter().GetResult();
                return response.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            }
        }

        private List<CodeOwnerEntry> ReadOwnersFromFile()
        {
            List<CodeOwnerEntry> entries = new List<CodeOwnerEntry>();
            string line;
            using (StringReader sr = new StringReader(GetFileContents(_codeownersFile)))
            {
                while ((line = sr.ReadLine()) != null)
                {
                    // Does the line start with '# PRLabel: "label1", "label2"

                    // Remove tabs and trim extra whitespace
                    line = NormalizeLine(line);

                    // Empty line, move on
                    if (string.IsNullOrEmpty(line))
                    {
                        continue;
                    }

                    CodeOwnerEntry entry = new CodeOwnerEntry();

                    // if we have the moniker in the line, parse the labels
                    if (line.StartsWith('#') &&                        
                        line.IndexOf(CodeOwnerEntry.LabelMoniker, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        entry.ParseLabels(line);

                        // We need to read the next line
                        line = sr.ReadLine();

                        if (line == null)
                        {
                            break;
                        }

                        // Remove tabs and trim extra whitespace
                        line = NormalizeLine(line);
                    }

                    // If this is not a comment line.
                    if (line.IndexOf('#') == -1)
                    {
                        entry.ParseOwnersAndPath(line);
                    }

                    if (entry.IsValid)
                    {
                        entries.Add(entry);
                    }
                }
            }
            return entries;
        }

        private static string NormalizeLine(string line)
        {
            // Remove tabs and trim extra whitespace
            return line.Replace('\t', ' ').Trim();
        }

        public override string GetTaskId()
        {
            return $"AzureSDKPullRequestLabelFolder_{_owner}_{_repo}";
        }
    }
}
