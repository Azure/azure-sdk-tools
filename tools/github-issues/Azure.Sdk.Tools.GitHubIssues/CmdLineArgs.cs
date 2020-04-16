using CommandLine.Attributes;
using GitHubIssues;
using System.Collections.Generic;

namespace Creator
{
    internal class CmdLineArgs
    {
        [OptionalArgument(null, "token", "The GitHub authentication token. If not specified, we will authorize to GitHub.")]
        public string Token { get; set; }

        [OptionalArgument(null, "emailToken", "The SendGrid authentication token. If not specified, no email will be sent")]
        public string EmailToken { get; set; }

        [OptionalArgument(null, "repositories", "The list of repositories where to add the milestones to. The format is: owner\\repoName\\email,email,email;owner\\repoName\\email,email,email")]
        public string Repositories { get; set; }

        [OptionalArgument(null, "from", "The email address to use when sending the email")]
        public string FromEmail { get; set; }

        public IEnumerable<RepositoryConfig> RepositoriesList => ParseRepositories(Repositories);

        private IEnumerable<RepositoryConfig> ParseRepositories(string repositories)
        {
            string[] repos = repositories.Split(';');

            foreach (var repo in repos)
            {
                yield return RepositoryConfig.Create(repo);
            }
        }
    }
}

