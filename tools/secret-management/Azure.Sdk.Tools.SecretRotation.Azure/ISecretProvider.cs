using Azure.Core;

namespace Azure.Sdk.Tools.SecretRotation.Azure;

public interface ISecretProvider
{
    Task<string> GetSecretValueAsync(TokenCredential credential, Uri secretUri);
}
