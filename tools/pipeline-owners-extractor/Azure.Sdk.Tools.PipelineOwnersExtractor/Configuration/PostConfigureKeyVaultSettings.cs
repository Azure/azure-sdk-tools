using System;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Azure.Sdk.Tools.PipelineOwnersExtractor.Configuration
{
    public class PostConfigureKeyVaultSettings<T> : IPostConfigureOptions<T> where T : class
    {
        private static readonly Regex secretRegex = new Regex(@"(?<vault>https://[a-zA-Z0-9-]+\.vault\.azure\.net)/secrets/(?<secret>.*)", RegexOptions.Compiled, TimeSpan.FromSeconds(5));
        private readonly ILogger logger;
        private readonly ISecretClientProvider secretClientProvider;

        public PostConfigureKeyVaultSettings(ILogger<PostConfigureKeyVaultSettings<T>> logger, ISecretClientProvider secretClientProvider)
        {
            this.logger = logger;
            this.secretClientProvider = secretClientProvider;
        }

        public void PostConfigure(string name, T options)
        {
            var stringProperties = typeof(T)
                .GetProperties()
                .Where(x => x.PropertyType == typeof(string));

            foreach (var property in stringProperties)
            {
                var value = (string)property.GetValue(options);

                if (value != null)
                {
                    var match = secretRegex.Match(value);

                    if (match.Success)
                    {
                        var vaultUrl = match.Groups["vault"].Value;
                        var secretName = match.Groups["secret"].Value;

                        try
                        {
                            var secretClient = this.secretClientProvider.GetSecretClient(new Uri(vaultUrl));
                            this.logger.LogInformation("Replacing setting property {PropertyName} with value from secret {SecretUrl}", property.Name, value);

                            var response = secretClient.GetSecret(secretName);
                            var secret = response.Value;

                            property.SetValue(options, secret.Value);
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
}
