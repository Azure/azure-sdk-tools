using CreateRuleFabricBot.Rules.PullRequestLabel;
using OutputColorizer;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

                if (entries[i].Labels.Count==0)
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
