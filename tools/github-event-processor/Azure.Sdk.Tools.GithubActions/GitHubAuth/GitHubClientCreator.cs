using System;
using System.Collections.Generic;
using System.Text;
using Octokit;

namespace Azure.Sdk.Tools.GithubEventProcessor.GitHubAuth
{
    public class GitHubClientCreator
    {
        /// <summary>
        /// Return a GitHubClient created with a GitHubToken pulled from the environment
        /// </summary>
        /// <param name="productHeaderName">This is used to generate the User Agent string sent with each request. The name used should represent the product, the GitHub Organization, or the GitHub username that's using Octokit.net (in that order of preference).</param>
        /// <returns></returns>
        public static GitHubClient createClientWithGitHubEnvToken(string productHeaderName)
        {
            if (String.IsNullOrEmpty(productHeaderName))
            {
                throw new ArgumentException("productHeaderName cannot be null or empty");
            }
            string githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
            if (String.IsNullOrEmpty(githubToken))
            {
                throw new ApplicationException("GITHUB_TOKEN cannot be null or empty");
            }
            var gitHubClient = new GitHubClient(new ProductHeaderValue(productHeaderName))
            {
                Credentials = new Credentials(githubToken)
            };
            return gitHubClient;
        }

        public static GitHubClient createClientWithLoginAndPassword(string login, string password, string productHeaderName)
        {
            if (String.IsNullOrEmpty(productHeaderName))
            {
                throw new ArgumentException("productHeaderName cannot be null or empty");
            }
            var gitHubClient = new GitHubClient(new ProductHeaderValue(productHeaderName))
            {
                Credentials = new Credentials(login, password)
            };
            return gitHubClient;
        }
    }
}
