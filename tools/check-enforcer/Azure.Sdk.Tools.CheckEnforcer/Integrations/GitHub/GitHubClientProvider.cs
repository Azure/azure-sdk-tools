using Azure.Core;
using Azure.Identity;
using Azure.Sdk.Tools.CheckEnforcer.Configuration;
using Azure.Sdk.Tools.CheckEnforcer.Integrations.GitHub;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using Octokit;
using System;
using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.CheckEnforcer
{
    public class GitHubClientProvider : IGitHubClientProvider
    {
        public GitHubClientProvider(IGlobalConfigurationProvider globalConfigurationProvider, IMemoryCache cache)
        {
            this.globalConfigurationProvider = globalConfigurationProvider;
            this.cache = cache;
        }

        private IGlobalConfigurationProvider globalConfigurationProvider;
        private IMemoryCache cache;

        private async Task<string> GetTokenAsync(CancellationToken cancellationToken)
        {
            var cachedApplicationToken = await cache.GetOrCreateAsync<string>("applicationTokenCacheKey", async (entry) =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(Constants.ApplicationTokenLifetimeInMinutes - 1);

                var headerAndPayloadString = GenerateJwtTokenHeaderAndPayload();
                var digest = ComputeHeaderAndPayloadDigest(headerAndPayloadString);
                var encodedSignature = await SignHeaderAndPayloadDigestWithGitHubApplicationKey(digest, cancellationToken);
                var applicationToken = AppendSignatureToHeaderAndPayload(headerAndPayloadString, encodedSignature);
                return applicationToken;
            });

            return cachedApplicationToken;
        }

        private string AppendSignatureToHeaderAndPayload(string headerAndPayloadString, string encodedSignature)
        {
            return $"{headerAndPayloadString}.{encodedSignature}";
        }

        private async Task<string> SignHeaderAndPayloadDigestWithGitHubApplicationKey(byte[] digest, CancellationToken cancellationToken)
        {
            var cryptographyClient = await GetCryptographyClient(cancellationToken);
            var signResult = await cryptographyClient.SignAsync(
                SignatureAlgorithm.RS256,
                digest,
                cancellationToken
                );

            var encodedSignature = Base64UrlEncoder.Encode(signResult.Signature);
            return encodedSignature;
        }

        private byte[] ComputeHeaderAndPayloadDigest(string headerAndPayloadString)
        {
            var headerAndPayloadBytes = Encoding.UTF8.GetBytes(headerAndPayloadString);

            var sha256 = new SHA256CryptoServiceProvider();
            var digest = sha256.ComputeHash(headerAndPayloadBytes);
            return digest;
        }

        private string GenerateJwtTokenHeaderAndPayload()
        {
            var jwtHeader = new JwtHeader();
            jwtHeader["alg"] = "RS256";

            var jwtPayload = new JwtPayload(
                issuer: globalConfigurationProvider.GetApplicationID(),
                audience: null,
                claims: null,
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddMinutes(Constants.ApplicationTokenLifetimeInMinutes),
                issuedAt: DateTime.UtcNow
                );

            var jwtToken = new JwtSecurityToken(jwtHeader, jwtPayload);
            var headerAndPayloadString = $"{jwtToken.EncodedHeader}.{jwtToken.EncodedPayload}";
            return headerAndPayloadString;
        }

        private async Task<CryptographyClient> GetCryptographyClient(CancellationToken cancellationToken)
        {
            // Using DefaultAzureCredential to support local development. If developing
            // locally you'll need to register an AAD application and set the following
            // variables:
            //
            //      AZURE_TENANT_ID (the ID of the AAD tenant)
            //      AZURE_CLIENT_ID (the iD of the AAD application you registered)
            //      AZURE_CLIENT_SECRET (the secret for the AAD application you registered)
            //
            // You can get these values when you configure the application. Set them in
            // the Debug section of the project properties. Once this is done you will need
            // to create a KeyVault instance and then register a GitHub application and upload
            // the private key into the vault. The AAD application that you just created needs
            // to have Get and Sign rights - so set an access policy up which grants the app
            // those rights.
            //
            var credential = new DefaultAzureCredential();

            var keyClient = GetKeyClient(credential);
            var key = await GetKey(keyClient, cancellationToken);

            var cryptographyClient = new CryptographyClient(key.Id, credential);
            return cryptographyClient;
        }

        private async Task<KeyVaultKey> GetKey(KeyClient keyClient, CancellationToken cancellationToken)
        {
            var keyResponse = await keyClient.GetKeyAsync(
                globalConfigurationProvider.GetGitHubAppPrivateKeyName(),
                cancellationToken: cancellationToken
                );

            var key = keyResponse.Value;
            return key;
        }

        private KeyClient GetKeyClient(TokenCredential credential)
        {
            var keyVaultUri = new Uri(globalConfigurationProvider.GetKeyVaultUri());
            var keyClient = new KeyClient(keyVaultUri, credential);
            return keyClient;
        }

        public async Task<GitHubClient> GetApplicationClientAsync(CancellationToken cancellationToken)
        {
            var token = await GetTokenAsync(cancellationToken);

            var appClient = new GitHubClient(new ProductHeaderValue(globalConfigurationProvider.GetApplicationName()))
            {
                Credentials = new Credentials(token, AuthenticationType.Bearer)
            };

            return appClient;
        }

        private ConcurrentDictionary<long, Octokit.AccessToken> cachedInstallationTokens = new ConcurrentDictionary<long, Octokit.AccessToken>();

        private async Task<string> GetInstallationTokenAsync(long installationId, CancellationToken cancellationToken)
        {
            var installationTokenCacheKey = $"{installationId}_installationTokenCacheKey";

            var cachedInstallationToken = await cache.GetOrCreateAsync<Octokit.AccessToken>(installationTokenCacheKey, async (entry) =>
            {
                var appClient = await GetApplicationClientAsync(cancellationToken);
                var installationToken = await appClient.GitHubApps.CreateInstallationToken(installationId);
                return installationToken;
            });

            return cachedInstallationToken.Token;
        }

        public async Task<GitHubClient> GetInstallationClientAsync(long installationId, CancellationToken cancellationToken)
        {
            var installationToken = await GetInstallationTokenAsync(installationId, cancellationToken);
            var installationClient = new GitHubClient(new ProductHeaderValue($"{globalConfigurationProvider.GetApplicationName()}-{installationId}"))
            {
                Credentials = new Credentials(installationToken)
            };

            return installationClient;
        }
    }
}
