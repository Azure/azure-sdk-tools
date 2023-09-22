using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.SecretRotation.Configuration;
using Azure.Sdk.Tools.SecretRotation.Core;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.SecretRotation.Stores.Generic;

public class RandomStringGenerator : SecretStore
{
    public const string MappingKey = "Random String";
    private readonly List<string> characterClasses;
    private readonly int length;
    private readonly ILogger logger;

    public RandomStringGenerator(int length, bool useLowercase, bool useUpperCase, bool useNumbers, bool useSpecial,
        ILogger logger)
    {
        this.length = length;
        this.logger = logger;

        this.characterClasses = new List<string>();

        if (useLowercase)
        {
            this.characterClasses.Add("abcdefghijklmnopqrstuvqxyz");
        }

        if (useUpperCase)
        {
            this.characterClasses.Add("ABCDEFGHIJKLMNOPQRSTUVQXYZ");
        }

        if (useNumbers)
        {
            this.characterClasses.Add("1234567890");
        }

        if (useSpecial)
        {
            this.characterClasses.Add("!@#$%^&*()");
        }

        if (this.characterClasses.Count == 0)
        {
            throw new ArgumentException("No character classes enabled for RandomStringGenerator");
        }
    }

    public override bool CanOriginate => true;

    public static Func<StoreConfiguration, SecretStore> GetSecretStoreFactory(ILogger logger)
    {
        return configuration =>
        {
            var parameters = configuration.Parameters?.Deserialize<Parameters>();

            if (parameters?.Length == null)
            {
                throw new RotationConfigurationException("Missing required parameter 'length'");
            }

            return new RandomStringGenerator(
                parameters.Length.Value,
                parameters.UseLowercase,
                parameters.UseUppercase,
                parameters.UseNumbers,
                parameters.UseSpecialCharacters,
                logger);
        };
    }

    public override Task<SecretValue> OriginateValueAsync(SecretState currentState, DateTimeOffset expirationDate,
        bool whatIf)
    {
        // Add all the in-play character classes into a common set and generate a high entropy string
        char[] availableCharacters = this.characterClasses.SelectMany(x => x).ToArray();

        char[] resultCharacters = new char[this.length];

        for (int i = 0; i < this.length; i++)
        {
            resultCharacters[i] = availableCharacters[Random.Shared.Next(availableCharacters.Length)];
        }

        // To ensure all classes are represented, allow each class to write a random character to a random index
        // without reusing any index
        List<int> characterIndices = Enumerable.Range(0, resultCharacters.Length).ToList();

        foreach (string characterClass in this.characterClasses)
        {
            // pick a random available index
            int randomCharacterIndex = characterIndices[Random.Shared.Next(characterIndices.Count)];

            // pick a random character from the current character set
            char randomCharacter = characterClass[Random.Shared.Next(characterClass.Length)];

            // place the character at the index
            resultCharacters[randomCharacterIndex] = randomCharacter;

            // remove the index from the list of available indices
            characterIndices.Remove(randomCharacterIndex);

            // If our string length is less than the number of character classes in play, we will run out of indices.
            if (characterIndices.Count == 0)
            {
                break;
            }
        }

        return Task.FromResult(new SecretValue { ExpirationDate = default, Value = new string(resultCharacters) });
    }

    private class Parameters
    {
        [JsonPropertyName("length")]
        public int? Length { get; set; }

        [JsonPropertyName("useLowercase")]
        public bool UseLowercase { get; set; }

        [JsonPropertyName("useUppercase")]
        public bool UseUppercase { get; set; }

        [JsonPropertyName("useNumbers")]
        public bool UseNumbers { get; set; }

        [JsonPropertyName("useSpecialCharacters")]
        public bool UseSpecialCharacters { get; set; }
    }
}
