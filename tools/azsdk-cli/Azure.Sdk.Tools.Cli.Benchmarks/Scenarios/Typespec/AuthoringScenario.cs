using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Sdk.Tools.Cli.Benchmarks.Infrastructure;
using Azure.Sdk.Tools.Cli.Benchmarks.Models;

namespace Azure.Sdk.Tools.Cli.Benchmarks.Scenarios.Typespec
{
    public class AuthoringScenario: BenchmarkScenario
    {
        /// <inheritdoc />
        public override string Name { get; }

        /// <inheritdoc />
        public override string Description { get; }

        /// <inheritdoc />
        public override string Prompt { get; }  
        public AuthoringScenario(string name, string description, string prompt) {
            Name = name;
            Description = description ?? string.Empty;
            Prompt = prompt ?? string.Empty;
        }

        /// <inheritdoc />
        public override RepoConfig Repo => new()
        {
            Owner = "Azure",
            Name = "azure-rest-api-specs",
            Ref = "main"
        };

        /// <inheritdoc />
        public override TimeSpan Timeout => TimeSpan.FromMinutes(3);

        /// <inheritdoc />
        public override async Task SetupAsync(Workspace workspace)
        {
            await workspace.RunCommandAsync("npm", "ci");
            /** start the azure knowledge service. **/
            await workspace.RunCommandAsync("");
        }
    }
}
