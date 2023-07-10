using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.SecretRotation.Configuration;
using Azure.Sdk.Tools.SecretRotation.Core;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.SecretRotation.Stores.Generic;

public class ManualActionStore : SecretStore
{
    public const string MappingKey = "Manual Action";
    private readonly ILogger logger;
    private readonly IUserValueProvider valueProvider;
    private readonly string prompt;

    public ManualActionStore(ILogger logger, IUserValueProvider valueProvider, string prompt)
    {
        this.logger = logger;
        this.valueProvider = valueProvider;
        this.prompt = prompt;
    }

    public override bool CanOriginate => true;

    public override bool CanWrite => true;

    public static Func<StoreConfiguration, SecretStore> GetSecretStoreFactory(ILogger logger, IUserValueProvider valueProvider)
    {
        return configuration =>
        {
            var parameters = configuration.Parameters?.Deserialize<Parameters>();

            if (parameters?.Prompt == null)
            {
                throw new RotationConfigurationException("Missing required parameter 'prompt'");
            }

            return new ManualActionStore(logger, valueProvider, parameters.Prompt);
        };
    }

    public override Task<SecretValue> OriginateValueAsync(SecretState currentState,
        DateTimeOffset expirationDate,
        bool whatIf)
    {
        string filledPrompt = FillPromptTokens(expirationDate);

        this.valueProvider.PromptUser(filledPrompt, oldValue: currentState.Value);

        string newValue = GetNewValueFromUser();

        DateTimeOffset newExpirationDate = GetExpirationDateFromUser();

        return Task.FromResult(new SecretValue { ExpirationDate = newExpirationDate, Value = newValue });
    }

    public override Task WriteSecretAsync(SecretValue secretValue,
        SecretState currentState,
        DateTimeOffset? revokeAfterDate,
        bool whatIf)
    {
        string filledPrompt = FillPromptTokens(secretValue.ExpirationDate);

        this.valueProvider.PromptUser(filledPrompt, oldValue: currentState.Value, newValue: secretValue.Value);

        return Task.CompletedTask;
    }

    private string FillPromptTokens(DateTimeOffset? expirationDate)
    {
        string targetDate = expirationDate.HasValue ? expirationDate.Value.ToString("o") : "<No value provided>";

        return this.prompt.Replace("{{TargetDate}}", targetDate);
    }

    private string GetNewValueFromUser()
    {
        while (true)
        {
            string? newValue = this.valueProvider.GetValue("Secret Value", secret: true);

            if (string.IsNullOrEmpty(newValue))
            {
                this.logger.LogInformation("The value cannot be a null or empty string.");
            }
            else
            {
                return newValue;
            }
        }
    }

    private DateTimeOffset GetExpirationDateFromUser()
    {
        while (true)
        {
            string? newDateString = this.valueProvider.GetValue("Expiration Date");

            if (DateTimeOffset.TryParse(newDateString, out var parsed))
            {
                return parsed;
            }

            this.logger.LogInformation("Unable to parse date string.");
        }
    }

    private class Parameters
    {
        [JsonPropertyName("prompt")]
        public string? Prompt { get; set; }
    }
}
