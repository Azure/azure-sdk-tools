using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Azure.Sdk.Tools.PipelineWitness.Configuration;
using Microsoft.Extensions.Options;
using Octokit;

namespace Azure.Sdk.Tools.PipelineWitness.GitHubActions;

public class GitHubCredentialStore : ICredentialStore
{
    private readonly TimeSpan processTimeout = TimeSpan.FromSeconds(13);
    private readonly PipelineWitnessSettings settings;

    public GitHubCredentialStore(IOptions<PipelineWitnessSettings> options)
    {
        this.settings = options.Value;
    }

    public async Task<Credentials> GetCredentials()
    {
        return string.IsNullOrEmpty(this.settings.GitHubAccessToken)
            ? await GetCliCredentialsAsync()
            : new Credentials(this.settings.GitHubAccessToken);
    }

    private async Task<Credentials> GetCliCredentialsAsync()
    {
        Process process = new()
        {
            StartInfo = GetAzureCliProcessStartInfo(),
            EnableRaisingEvents = true
        };

        using ProcessRunner processRunner = new(process, this.processTimeout, CancellationToken.None);

        string output = await processRunner.RunAsync().ConfigureAwait(false);

        return new Credentials(output, AuthenticationType.Bearer);
    }

    private static ProcessStartInfo GetAzureCliProcessStartInfo()
    {
        string environmentPath = Environment.GetEnvironmentVariable("PATH");

        string command = "gh auth token";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string programFiles = Environment.GetEnvironmentVariable("ProgramFiles");
            string programFilesx86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
            string defaultPath = $"{programFilesx86}\\GitHub CLI;{programFiles}\\GitHub CLI";

            return new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"),
                Arguments = $"/d /c \"{command}\"",
                UseShellExecute = false,
                ErrorDialog = false,
                CreateNoWindow = true,
                WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System),
                Environment = { { "PATH", !string.IsNullOrEmpty(environmentPath) ? environmentPath : defaultPath } }
            };
        }
        else
        {
            string defaultPath = "/usr/bin:/usr/local/bin";

            return new ProcessStartInfo
            {
                FileName = "/bin/sh",
                Arguments = $"-c \"{command}\"",
                UseShellExecute = false,
                ErrorDialog = false,
                CreateNoWindow = true,
                WorkingDirectory = "/bin/",
                Environment = { { "PATH", !string.IsNullOrEmpty(environmentPath) ? environmentPath : defaultPath } }
            };
        }
    }
}
