using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Azure.Sdk.Tools.PipelineWitness.Configuration;

public partial class PostConfigureKeyVaultSettings<T> : IPostConfigureOptions<T> where T : class
{
    [GeneratedRegex(@"^(?<vault>https://.*?\.vault\.azure\.net)/secrets/(?<secret>.*)$")]
    private static partial Regex SecretUriRegex(); 
    
    private readonly ILogger logger;
    private readonly ISecretClientProvider secretClientProvider;
    private readonly Dictionary<string, (DateTimeOffset ExpirationTime, string Value)> valueCache;

    public PostConfigureKeyVaultSettings(ILogger<PostConfigureKeyVaultSettings<T>> logger, ISecretClientProvider secretClientProvider)
    {
        this.logger = logger;
        this.secretClientProvider = secretClientProvider;
        this.valueCache = [];
    }

    public void PostConfigure(string name, T options)
    {
        var stringProperties = typeof(T)
            .GetProperties()
            .Where(x => x.PropertyType == typeof(string));

        foreach (var property in stringProperties)
        {
            var propertyValue = (string)property.GetValue(options);

            if (propertyValue != null)
            {
                if(this.valueCache.TryGetValue(propertyValue, out var cacheEntry))
                {
                    if (DateTimeOffset.UtcNow < cacheEntry.ExpirationTime)
                    {
                        this.logger.LogInformation("Replacing setting {PropertyName} with value from cache", property.Name);
                        property.SetValue(options, cacheEntry.Value);
                        continue;
                    }
                }

                var match = SecretUriRegex().Match(propertyValue);

                if (match.Success)
                {
                    var vaultUrl = match.Groups["vault"].Value;
                    var secretName = match.Groups["secret"].Value;

                    this.logger.LogInformation("Setting {PropertyName} points to Key Vault secret url {SecretUrl}", property.Name, propertyValue);
                    try
                    {
                        var secretClient = this.secretClientProvider.GetSecretClient(new Uri(vaultUrl));

                        this.logger.LogInformation("Getting secret value from {SecretUrl}", propertyValue);
                        var response = secretClient.GetSecret(secretName);
                        var secret = response.Value;

                        this.logger.LogInformation("Replacing setting {PropertyName} with value from secret", property.Name);
                        property.SetValue(options, secret.Value);

                        this.logger.LogInformation("Caching secret value for setting {PropertyName}", property.Name);
                        this.valueCache[propertyValue] = (ExpirationTime: DateTimeOffset.UtcNow.AddMinutes(5), secret.Value);
                    }
                    catch (Exception exception)
                    {
                        this.logger.LogError(exception, "Unable to read secret {SecretName} from vault {VaultUrl}", secretName, vaultUrl);
                    }
                }
            }
        }
    }
}
