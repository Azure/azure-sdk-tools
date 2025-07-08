using Azure.Core;
using Azure.Identity;

namespace Azure.Tools.GeneratorAgent.Authentication
{
    public interface ICredentialFactory
    {
        TokenCredential CreateCredential(RuntimeEnvironment environment, TokenCredentialOptions? options = null);
    }
}
