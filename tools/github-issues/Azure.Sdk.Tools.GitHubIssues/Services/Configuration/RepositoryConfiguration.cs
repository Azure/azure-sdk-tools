namespace Azure.Sdk.Tools.GitHubIssues.Services.Configuration
{
    public class RepositoryConfiguration
    {
        public string Owner { get; set; }

        public string Name { get; set; }

        public string[] ToEmail { get; set; }

        public string[] CcEmail { get; set; }

        public static RepositoryConfiguration Create(string entry)
        {
            // the entry looks like:
            // repo\owner\to:alias,alias#cc:alias

            // normalize the slashes.

            entry = entry.Replace('/', '\\');

            string[] entries = entry.Split('\\');


            // parse the emails.
            string[] emails = entries[2].Split("#");

            string to = string.Empty, cc = string.Empty;

            for (int i = 0; i < emails.Length; i++)
            {
                if (emails[i].StartsWith("to"))
                {
                    to += emails[i];
                }
                else if (emails[i].StartsWith("cc"))
                {
                    cc += emails[i];
                }
            }

            RepositoryConfiguration config = new RepositoryConfiguration
            {
                Owner = entries[0],
                Name = entries[1],
                ToEmail = ParseEmails(to),
                CcEmail = ParseEmails(cc)
            };

            return config;
        }

        private static string[] ParseEmails(string aliasList)
        {
            // aliasList looks like: to:alias,alias or cc:alias,alias
            aliasList = aliasList.Substring(aliasList.IndexOf(":") + 1);

            string[] aliases = aliasList.Split(",");

            // the assumption is that all email addresses are going to Microsoft.
            for (int i = 0; i < aliases.Length; i++)
            {
                aliases[i] = aliases[i] + "@microsoft.com";
            }

            return aliases;
        }
    }
}
