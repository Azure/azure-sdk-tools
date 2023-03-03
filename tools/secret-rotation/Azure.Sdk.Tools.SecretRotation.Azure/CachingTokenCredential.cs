using System.Collections.Concurrent;
using Azure.Core;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.SecretRotation.Azure;

public class CachingTokenCredential : TokenCredential
{
    private readonly ILogger logger;
    private readonly TokenCredential innerCredential;
    private readonly ConcurrentDictionary<string, AccessToken> cachedTokens = new();


    public CachingTokenCredential(ILogger logger, TokenCredential innerCredential)
    {
        this.logger = logger;
        this.innerCredential = innerCredential;
    }

    public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext,
        CancellationToken cancellationToken)
    {
        string scopes = string.Join(",", requestContext.Scopes);

        this.logger.LogDebug("Begin: GetTokenAsync scopes: {Scopes}, claims: {Claims}", scopes, requestContext.Claims);

        string cacheKey = GetCacheKey(requestContext);

        if (!TryGetCachedToken(cacheKey, out AccessToken accessToken))
        {
            this.logger.LogDebug("Requesting token");
            AccessToken newToken = await this.innerCredential.GetTokenAsync(requestContext, cancellationToken);

            accessToken = CacheToken(cacheKey, newToken);

        }

        this.logger.LogDebug("End: GetTokenAsync scopes: {Scopes}, claims: {Claims}", scopes, requestContext.Claims);
        
        return accessToken;
    }

    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        string scopes = string.Join(",", requestContext.Scopes);

        this.logger.LogDebug("Begin: GetToken scopes: {Scopes}, claims: {Claims}", scopes, requestContext.Claims);

        string cacheKey = GetCacheKey(requestContext);

        if (!TryGetCachedToken(cacheKey, out AccessToken accessToken))
        {
            this.logger.LogDebug("Requesting token");
            AccessToken newToken = this.innerCredential.GetToken(requestContext, cancellationToken);

            accessToken = CacheToken(cacheKey, newToken);
        }

        this.logger.LogDebug("End: GetToken scopes: {Scopes}, claims: {Claims}", scopes, requestContext.Claims);

        return accessToken;
    }

    private bool TryGetCachedToken(string cacheKey, out AccessToken accessToken)
    {
        if (this.cachedTokens.TryGetValue(cacheKey, out AccessToken cachedToken) && cachedToken.ExpiresOn > DateTimeOffset.UtcNow)
        {
            accessToken = cachedToken;
            return true;
        }

        accessToken = default;
        return false;
    }

    private AccessToken CacheToken(string cacheKey, AccessToken newToken)
    {
        this.logger.LogDebug("Caching token");

        AccessToken accessToken = this.cachedTokens.AddOrUpdate(
            cacheKey,
            newToken,
            (_, existingToken) => newToken.ExpiresOn > existingToken.ExpiresOn ? newToken : existingToken);

        return accessToken;
    }

    private static string GetCacheKey(TokenRequestContext requestContext)
    {
        return $"{requestContext.TenantId}|{string.Join(",", requestContext.Scopes)}|{requestContext.Claims}";
    }
}
