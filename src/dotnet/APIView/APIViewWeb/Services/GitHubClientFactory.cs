// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Azure;
using Azure.Identity;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Octokit;

namespace APIViewWeb.Services;

public class GitHubClientFactory: IGitHubClientFactory
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<GitHubClientFactory> _logger;
    private readonly ProductHeaderValue _productHeaderValue;
    private readonly ChainedTokenCredential _credential;
    private readonly ConcurrentDictionary<string, CachedGitHubClient> _clientCache;

    private class CachedGitHubClient
    {
        public GitHubClient Client { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
    }

    public GitHubClientFactory(IConfiguration configuration, ILogger<GitHubClientFactory> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _productHeaderValue = new ProductHeaderValue("apiview");
        _credential = new ChainedTokenCredential(
            new ManagedIdentityCredential(),    
            new AzureCliCredential(),
            new AzureDeveloperCliCredential());
        _clientCache = new ConcurrentDictionary<string, CachedGitHubClient>();
    }

    public async Task<GitHubClient> CreateGitHubClientAsync(string owner, string repository)
    {
        string cacheKey = $"{owner}/{repository}";

        if (_clientCache.TryGetValue(cacheKey, out CachedGitHubClient cachedClient))
        {
            // Add a 5-minute buffer before expiration to ensure token remains valid during use
            if (cachedClient.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(5))
            {
                return cachedClient.Client;
            }
            else
            {
                _clientCache.TryRemove(cacheKey, out _);
            }
        }

        string appId = _configuration["GitHubApp:Id"];
        string keyVaultUrl = _configuration["GitHubApp:KeyVaultUrl"];
        string keyName = _configuration["GitHubApp:KeyName"];

        if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(keyVaultUrl) || string.IsNullOrEmpty(keyName))
        {
            _logger.LogWarning("GitHub App credentials not configured. GitHub client will not be available. " +
                               "Required: GitHubApp:Id, GitHubApp:KeyVaultUrl, GitHubApp:KeyName");
            return null;
        }

        try
        {
            string jwt = await CreateGitHubAppJwtAsync(keyVaultUrl, keyName, appId);
            if (string.IsNullOrEmpty(jwt))
            {
                _logger.LogError("Failed to generate GitHub App JWT token.");
                return null;
            }

            GitHubClient appClient = new(_productHeaderValue)
            {
                Credentials = new Credentials(jwt, AuthenticationType.Bearer)
            };

            Installation installation;

            try
            {
                installation = await appClient.GitHubApps.GetRepositoryInstallationForCurrent(owner, repository);
            }
            catch (NotFoundException)
            {
                _logger.LogError("The GitHub App is not installed on the repository {Owner}/{Repository}.", owner,
                    repository);
                return null;
            }

            AccessToken accessToken = await appClient.GitHubApps.CreateInstallationToken(installation.Id);
            GitHubClient installationClient = new(_productHeaderValue)
            {
                Credentials = new Credentials(accessToken.Token)
            };

            var cachedClientEntry = new CachedGitHubClient
            {
                Client = installationClient,
                ExpiresAt = accessToken.ExpiresAt
            };
            _clientCache.TryAdd(cacheKey, cachedClientEntry);

            return installationClient;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create GitHub client for {Owner}/{Repository}", owner, repository);
            return null;
        }
    }

    private async Task<string> CreateGitHubAppJwtAsync(string keyVaultUrl, string keyName, string appId)
    {
        try
        {
            var header = new { alg = "RS256", typ = "JWT" };
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var payload = new
            {
                iat = now,
                exp = now + 600, // 10 minutes
                iss = appId
            };

            string headerJson = JsonSerializer.Serialize(header);
            string payloadJson = JsonSerializer.Serialize(payload);

            string encodedHeader = Base64UrlEncode(headerJson);
            string encodedPayload = Base64UrlEncode(payloadJson);
            string unsignedToken = $"{encodedHeader}.{encodedPayload}";

            KeyClient keyClient = new(new Uri(keyVaultUrl), _credential);
            Response<KeyVaultKey> key = await keyClient.GetKeyAsync(keyName);

            CryptographyClient cryptoClient = new(key.Value.Id, _credential);

            byte[] unsignedTokenBytes = Encoding.ASCII.GetBytes(unsignedToken);
            byte[] hash = SHA256.HashData(unsignedTokenBytes);

            SignResult signResult = await cryptoClient.SignAsync(SignatureAlgorithm.RS256, hash);
            string signature = Convert.ToBase64String(signResult.Signature)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');

            return $"{unsignedToken}.{signature}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create GitHub App JWT using Key Vault {KeyVaultUrl}/{KeyName}", keyVaultUrl,
                keyName);
            return null;
        }
    }

    private static string Base64UrlEncode(string input)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(input);
        string base64 = Convert.ToBase64String(bytes);
        return base64.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
