namespace Azure.Sdk.Tools.SecretRotation.Stores.Generic;

public interface IUserValueProvider
{
    string? GetValue(string prompt, bool secret = false);

    void PromptUser(string prompt, string? oldValue = default, string? newValue = default);
}
