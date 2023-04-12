using Octokit;

public class GitHubClient : IGitHubClient
{
    public Octokit.GitHubClient Client { get; }
    private Dictionary<(string, string), SecretsPublicKey> PublicKeyCache { get; }

    public GitHubClient(string? token)
    {
        Client = new Octokit.GitHubClient(new ProductHeaderValue("azsdk-access-manager"));
        if (token is not null)
        {
            Client.Credentials = new Credentials(token);
        }
        PublicKeyCache = new Dictionary<(string, string), SecretsPublicKey>();
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
    public async Task<string> SetRepositorySecret(string owner, string repo, string secretName, string secretValue)
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

        var secret = await Client.Repository.Actions.Secrets.CreateOrUpdate(owner, repo, secretName, upsertSecret);
        return secret.Name;
    }
}

public interface IGitHubClient
{
    Task<string> SetRepositorySecret(string owner, string repo, string secretName, string secretValue);
}