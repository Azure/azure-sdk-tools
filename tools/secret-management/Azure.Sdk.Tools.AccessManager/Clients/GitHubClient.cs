using System.Diagnostics;
using System.Text.RegularExpressions;
using Octokit;

namespace Azure.Sdk.Tools.AccessManager;

public class GitHubClient : IGitHubClient
{
    public Octokit.GitHubClient Client { get; }
    private Dictionary<(string, string), SecretsPublicKey> PublicKeyCache { get; }

    public GitHubClient()
    {
        PublicKeyCache = new Dictionary<(string, string), SecretsPublicKey>();
        Client = new Octokit.GitHubClient(new ProductHeaderValue("azsdk-access-manager"));
        var token = GetCredential();
        if (!string.IsNullOrEmpty(token))
        {
            Client.Credentials = new Credentials(token);
        }
    }

    public string GetCredential()
    {
        var githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (!string.IsNullOrEmpty(githubToken))
        {
            return githubToken;
        }

        string? token = null;
        string output = "";
        var process = new Process();
        process.StartInfo.FileName = "gh";
        process.StartInfo.Arguments = "auth status --show-token";
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;

        try
        {
            process.Start();
            output = process.StandardError.ReadToEnd().Trim();
            process.WaitForExit();
        }
        catch (Exception ex)
        {
            Console.WriteLine("WARNING: Exception while running `gh auth status --show-token` below. The command may not exist. Proceeding without github login.");
            Console.WriteLine("    " + ex.Message);
            return string.Empty;
        }

        if (!string.IsNullOrEmpty(output) && process.ExitCode == 0)
        {
            var match = Regex.Match(output, @"Token:\s(?<token>[_a-zA-Z0-9]+)");
            var details = Regex.Replace(output, @"Token:\s[_a-zA-Z0-9]+", "Token: <REDACTED>");
            Console.WriteLine($"{details}");
            token = match.Groups["token"]?.Value.Trim();
        }

        if (output is null || process.ExitCode != 0 || string.IsNullOrEmpty(token))
        {
            Console.WriteLine("WARNING: Set GITHUB_TOKEN environment variable from a PAT or run `gh auth login`. " +
                              "Operations will fail if githubRepositorySecrets is configured.");
            return string.Empty;
        }

        return token;
    }


    private async Task<SecretsPublicKey> GetRepoPublicKeyData(string owner, string repo)
    {
        if (PublicKeyCache.TryGetValue((owner, repo), out var publicKey))
        {
            return publicKey;
        }

        var publicKeyResponse = await Client.Repository.Actions.Secrets.GetPublicKey(owner, repo);
        PublicKeyCache[(owner, repo)] = publicKeyResponse;
        return publicKeyResponse;
    }

    // TODO: Support and use repository variables. Octokit does not have a client for this yet (4/12/2023).
    public async Task SetRepositorySecret(string owner, string repo, string secretName, string secretValue)
    {
        var publicKey = await GetRepoPublicKeyData(owner, repo);
        var secretBytes = System.Text.Encoding.UTF8.GetBytes(secretValue);
        var publicKeyEncoded = Convert.FromBase64String(publicKey.Key);
        var sealedPublicKeyBox = Sodium.SealedPublicKeyBox.Create(secretValue, publicKeyEncoded);
        var encryptedSecret = Convert.ToBase64String(sealedPublicKeyBox);

        var upsertSecret = new UpsertRepositorySecret
        {
            EncryptedValue = encryptedSecret,
            KeyId = publicKey.KeyId,
        };

        await Client.Repository.Actions.Secrets.CreateOrUpdate(owner, repo, secretName, upsertSecret);
    }
}

public interface IGitHubClient
{
    Task SetRepositorySecret(string owner, string repo, string secretName, string secretValue);
}