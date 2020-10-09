using CreateRuleFabricBot.Helpers;
using OutputColorizer;
using System.Collections.Generic;
using System.IO;

namespace CreateRuleFabricBot
{
    public static class CodeOwnersFile
    {
        public static List<CodeOwnerEntry> ParseFile(string filePathOrUrl)
        {
            string content;
            Colorizer.Write("Retrieving file content from [Yellow!{0}]... ", filePathOrUrl);
            content = FileHelpers.GetFileContents(filePathOrUrl);
            Colorizer.WriteLine("[Green!Done]");

            return ParseContent(content);
        }

        public static List<CodeOwnerEntry> ParseContent(string fileContent)
        {
            Colorizer.Write("Parsing CODEOWNERS table... ");
            List<CodeOwnerEntry> entries = new List<CodeOwnerEntry>();
            string line;
            using (StringReader sr = new StringReader(fileContent))
            {
                while ((line = sr.ReadLine()) != null)
                {
                    // Remove tabs and trim extra whitespace
                    line = NormalizeLine(line);

                    // Empty line, move on
                    if (string.IsNullOrEmpty(line))
                    {
                        continue;
                    }

                    CodeOwnerEntry entry = new CodeOwnerEntry();

                    // if we have a comment on the line AND if the line contains special delimiters
                    while (line.StartsWith('#') && entry.ProcessLabelsOnLine(line))
                    {
                        // We need to read the next line as we parsed this line
                        line = sr.ReadLine();

                        if (line == null)
                        {
                            break;
                        }

                        // Remove tabs and trim extra whitespace
                        line = NormalizeLine(line);
                    }

                    // Empty line, move on
                    if (string.IsNullOrEmpty(line))
                    {
                        continue;
                    }

                    // If this is not a comment line.
                    if (line.IndexOf('#') == -1 ||
                        line.IndexOf(CodeOwnerEntry.MissingFolder, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        entry.ParseOwnersAndPath(line);
                    }

                    if (entry.IsValid)
                    {
                        entries.Add(entry);
                    }
                }
            }
            Colorizer.WriteLine("[Green!Done]");
            return entries;
        }

        private static string NormalizeLine(string line)
        {
            // Remove tabs and trim extra whitespace
            return line.Replace('\t', ' ').Trim();
        }
    }
}
