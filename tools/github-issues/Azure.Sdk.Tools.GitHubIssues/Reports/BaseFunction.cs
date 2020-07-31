using CommandLine;
using Creator;
using GitHubIssues.Helpers;
using Microsoft.Extensions.Logging;
using Octokit;
using Polly;
using System;
using System.Threading;

namespace GitHubIssues
{
    abstract class BaseFunction
    {
        protected GitHubClient _gitHub;
        protected CmdLineArgs _cmdLine;
        protected ILogger _log;

        private static readonly ParserOptions s_parserOptions = new ParserOptions()
        {
            VariableNamePrefix = "parser_",
            LogParseErrorToConsole = true
        };

        public BaseFunction(ILogger log)
        {
            _log = log;

            _log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
            if (!Parser.TryParse(new string[0], out _cmdLine, s_parserOptions))
            {
                return;
            }
            log.LogInformation("Command Line arguments parsed!");

            // we have to get the token.
            _gitHub = GitHubHelpers.CreateGitHubClient(_cmdLine.Token);

            log.LogInformation("Created GitHub client");
        }

        public void Execute()
        {
            Policy.Handle<RateLimitExceededException>()
                .Retry(3, (ex, retryCount) =>
                {
                    _log.LogWarning("GitHub rate limit exceeded, retrying after 30 seconds.");
                    Thread.Sleep(30000);
                })
                .Execute(() =>
                {
                    ExecuteCore();
                });
        }

        protected abstract void ExecuteCore();
    }
}
