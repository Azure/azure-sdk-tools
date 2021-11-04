using OutputColorizer;
using System.Collections.Generic;
using System.IO;

namespace Azure.Sdk.Tools.CodeOwnersParser
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


            // An entry ends when we get to a path (a real path or a commented dummy path)

            using (StringReader sr = new StringReader(fileContent))
            {
                CodeOwnerEntry entry = new CodeOwnerEntry();

                // we are going to read line by line until we find a line that is not a comment OR that is using the placeholder entry inside the comment.
                // while we are trying to find the folder entry, we parse all comment lines to extract the labels from it.
                // when we find the path or placeholder, we add the completed entry and create a new one.
                while ((line = sr.ReadLine()) != null)
                {
                    line = NormalizeLine(line);

                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    if (!line.StartsWith("#") || line.IndexOf(CodeOwnerEntry.MissingFolder, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        // If this is not a comment line OR this is a placeholder entry

                        entry.ParseOwnersAndPath(line);

                        // only add it if it is a valid entry
                        if (entry.IsValid)
                        {
                            entries.Add(entry);
                        }

                        // create a new entry.
                        entry = new CodeOwnerEntry();
                    }
                    else if (line.StartsWith("#"))
                    {
                        // try to process the line in case there are markers that need to be extracted
                        entry.ProcessLabelsOnLine(line);
                    }

                }
            }
            Colorizer.WriteLine("[Green!Done]");
            return entries;
        }

        private static string NormalizeLine(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return line;
            }

            // Remove tabs and trim extra whitespace
            return line.Replace('\t', ' ').Trim();
        }
    }
}
