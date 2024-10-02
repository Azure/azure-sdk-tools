using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Sdk.Tools.PipelineWitness.Configuration;
using Azure.Sdk.Tools.PipelineWitness.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Octokit;

namespace Azure.Sdk.Tools.PipelineWitness.GitHubActions;

public class GitHubClientFactory
{
    private readonly TimeSpan processTimeout = TimeSpan.FromSeconds(13);
    private readonly PipelineWitnessSettings settings;
    private readonly ProductHeaderValue productHeaderValue;
    private readonly ILogger<GitHubClientFactory> logger;

    public GitHubClientFactory(ILogger<GitHubClientFactory> logger, IOptions<PipelineWitnessSettings> options)
    {
        this.settings = options.Value;

        string version = typeof(GitHubClientFactory).Assembly.GetName().Version.ToString();
        this.productHeaderValue = new("PipelineWitness", version);
        this.logger = logger;
    }

    public async Task<IGitHubClient> CreateGitHubClientAsync()
    {
        // If we're running in local dev mode, return a client based on the CLI token
        if (string.IsNullOrEmpty(this.settings.GitHubAppPrivateKey))
        {
            this.logger.LogDebug("No private key provided, creating cli authenticated client.");
            return await CreateGitHubClientWithCliTokenAsync();
        }

        this.logger.LogDebug("Creating app token authenticated client.");
        return CreateGitHubClientWithAppToken();
    }

    public async Task<IGitHubClient> CreateGitHubClientAsync(string owner, string repository)
    {
        // If we're running in local dev mode, return a client based on the CLI token
        if (string.IsNullOrEmpty(this.settings.GitHubAppPrivateKey))
        {
            this.logger.LogDebug("No private key provided, creating cli authenticated client.");
            return await CreateGitHubClientWithCliTokenAsync();
        }

        this.logger.LogDebug("Creating app token authenticated client.");
        GitHubClient appClient = CreateGitHubClientWithAppToken();

        Installation installation;
        
        try
        {
            this.logger.LogDebug("Getting app installation for {Owner}/{Repository}.", owner, repository);
            installation = await appClient.GitHubApps.GetRepositoryInstallationForCurrent(owner, repository);
        }
        catch (NotFoundException)
        {
            this.logger.LogError("The GitHub App is not installed on the repository {Owner}/{Repository}.", owner, repository);
            throw new InvalidOperationException($"The GitHub App is not installed on the repository {owner}/{repository}");
        }

        this.logger.LogDebug("Getting installation token for {Owner}/{Repository}.", owner, repository);
        AccessToken accessToken = await appClient.GitHubApps.CreateInstallationToken(installation.Id);

        this.logger.LogDebug("Creating installation token authenticated client.");
        Credentials installationCredentials = new(accessToken.Token);

        GitHubClient installationClient = new(this.productHeaderValue)
        {
            Credentials = installationCredentials
        };

        return installationClient;
    }

    private GitHubClient CreateGitHubClientWithAppToken()
    {
        Credentials credentials = CreateAppCredentials();

        GitHubClient client = new(this.productHeaderValue)
        {
            Credentials = credentials
        };

        return client;
    }

    private async Task<GitHubClient> CreateGitHubClientWithCliTokenAsync()
    {
        Credentials credentials = await GetCliCredentialsAsync();

        GitHubClient client = new(this.productHeaderValue)
        {
            Credentials = credentials
        };

        return client;
    }

    private Credentials CreateAppCredentials()
    {
        RSA rsa = RSA.Create();
        rsa.ImportFromPem(this.settings.GitHubAppPrivateKey);

        RsaSecurityKey securityKey = new(rsa);

        JsonWebTokenHandler handler = new()
        {
            SetDefaultTimesOnTokenCreation = false
        };

        DateTimeOffset issuedAt = DateTimeOffset.UtcNow.AddSeconds(-10);
        DateTimeOffset expires = issuedAt.AddMinutes(10);

        var payload = new
        {
            iat = issuedAt.ToUnixTimeSeconds(),
            exp = expires.ToUnixTimeSeconds(),
            iss = this.settings.GitHubAppClientId,
        };

        string accessToken = handler.CreateToken(JsonSerializer.Serialize(payload), new SigningCredentials(securityKey, "RS256"));

        return new Credentials(accessToken, AuthenticationType.Bearer);
    }

    private async Task<Credentials> GetCliCredentialsAsync()
    {
        this.logger.LogDebug("Creating GitHub token using gh cli.");
        Process process = new()
        {
            StartInfo = GetGitHubCliProcessStartInfo(),
            EnableRaisingEvents = true
        };

        using ProcessRunner processRunner = new(process, this.processTimeout, CancellationToken.None);

        string output = await processRunner.RunAsync().ConfigureAwait(false);

        return new Credentials(output, AuthenticationType.Bearer);
    }

    private static ProcessStartInfo GetGitHubCliProcessStartInfo()
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
