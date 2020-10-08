using OutputColorizer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;

namespace CreateRuleFabricBot
{
    public static class CodeOwners
    {
        public static List<CodeOwnerEntry> ReadOwnersFromFile(string filePathOrUrl)
        {
            string content;
            Colorizer.Write("Retrieving file content from [Yellow!{0}]... ", filePathOrUrl);
            content = GetFileContents(filePathOrUrl);
            Colorizer.WriteLine("[Green!Done]");

            return ParseOwnersFromContent(content);
        }

        public static List<CodeOwnerEntry> ParseOwnersFromContent(string fileContent)
        {
            Colorizer.Write("Parsing CODEOWNERS table... ");
            List<CodeOwnerEntry> entries = new List<CodeOwnerEntry>();
            string line;
            using (StringReader sr = new StringReader(fileContent))
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
            Colorizer.WriteLine("[Green!Done]");
            return entries;
        }

        private static string NormalizeLine(string line)
        {
            // Remove tabs and trim extra whitespace
            return line.Replace('\t', ' ').Trim();
        }

        private static string GetFileContents(string fileOrUri)
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
    }
}
