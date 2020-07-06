using CreateRuleFabricBot.Rules.PullRequestLabel;
using OutputColorizer;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

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
            ""exclude"": [ ]
          }
";

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

            for (int i = 0; i < entries.Count; i++)
            {
                // Entries with wildcards are not yet supported
                if (entries[i].ContainsWildcard)
                {
                    // log a warning there

                    if (entries[i].Labels.Any())
                    {
                        Colorizer.WriteLine("[Yellow!Warning]: The expression '{0}' contains a wildcard and a label '{1}' which is not supported!", entries[i].PathExpression, string.Join(',', entries[i].Labels));
                    }

                    continue; //TODO: regex expressions are not yet supported
                }

                // Entries with more than one label are not yet supported.
                if (entries[i].Labels.Count > 1)
                {
                    Colorizer.WriteLine("[Yellow!Warning]: Multiple labels for the same path are not yet supported");
                    continue;
                }

                // get the payload
                string entryPayload = ToConfigString(entries[i]);

                Colorizer.WriteLine("Found path '[Cyan!{0}]' with label '[Magenta!{1}]'", entries[i].PathExpression, entries[i].Labels.FirstOrDefault());

                configPayload.Append(ToConfigString(entries[i]));
            }

            // remove the trailing ','
            configPayload.Remove(configPayload.Length - 1, 1);

            // create the payload from the template
            return s_template
                .Replace("###taskId###", GetTaskId())
                .Replace("###labelConfig###", configPayload.ToString());
        }

        private string ToConfigString(CodeOwnerEntry entry)
        {
            string result = s_configTemplate;

            result = result.Replace("###label###", entry.Labels.First());
            result = result.Replace("###srcFolders###", $"\"{entry.PathExpression}\"");

            return result;
        }

        private List<CodeOwnerEntry> ReadOwnersFromFile()
        {
            List<CodeOwnerEntry> entries = new List<CodeOwnerEntry>();
            string line;
            using (StreamReader sr = new StreamReader(_codeownersFile))
            {
                while ((line = sr.ReadLine()) != null)
                {
                    CodeOwnerEntry entry = CodeOwnerEntry.Parse(line);
                    if (entry != null)
                    {
                        entries.Add(entry);
                    }
                }
            }
            return entries;
        }

        public override string GetTaskId()
        {
            return $"AzureSDKPullRequestLabelFolder_{_owner}_{_repo}";
        }
    }
}
