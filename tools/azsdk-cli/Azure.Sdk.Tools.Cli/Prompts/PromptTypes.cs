// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Prompts;



/// <summary>
/// Context interface for providing parameters to prompt templates.
/// </summary>
public interface IPromptContext
{
    /// <summary>
    /// Gets a parameter value by key.
    /// </summary>
    /// <param name="key">The parameter key</param>
    /// <returns>The parameter value or null if not found</returns>
    object? GetParameter(string key);

    /// <summary>
    /// Gets a strongly-typed parameter value by key.
    /// </summary>
    /// <typeparam name="T">The expected parameter type</typeparam>
    /// <param name="key">The parameter key</param>
    /// <returns>The parameter value or default if not found</returns>
    T? GetParameter<T>(string key);

    /// <summary>
    /// Sets a parameter value.
    /// </summary>
    /// <param name="key">The parameter key</param>
    /// <param name="value">The parameter value</param>
    void SetParameter(string key, object? value);

    /// <summary>
    /// Checks if a parameter exists.
    /// </summary>
    /// <param name="key">The parameter key</param>
    /// <returns>True if the parameter exists, false otherwise</returns>
    bool HasParameter(string key);

    /// <summary>
    /// Gets all parameter keys.
    /// </summary>
    /// <returns>Collection of all parameter keys</returns>
    IEnumerable<string> GetParameterKeys();
}

/// <summary>
/// Result of prompt template validation.
/// </summary>
public class PromptValidationResult
{
    /// <summary>
    /// Indicates if the validation passed.
    /// </summary>
    public bool IsValid { get; set; } = true;

    /// <summary>
    /// Collection of validation errors.
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Collection of validation warnings.
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Adds an error to the validation result.
    /// </summary>
    /// <param name="error">The error message</param>
    public void AddError(string error)
    {
        Errors.Add(error);
        IsValid = false;
    }

    /// <summary>
    /// Adds a warning to the validation result.
    /// </summary>
    /// <param name="warning">The warning message</param>
    public void AddWarning(string warning)
    {
        Warnings.Add(warning);
    }
}

/// <summary>
/// Simple implementation of IPromptContext using a dictionary.
/// </summary>
public class PromptContext : IPromptContext
{
    private readonly Dictionary<string, object?> _parameters = new();

    public object? GetParameter(string key)
    {
        _parameters.TryGetValue(key, out var value);
        return value;
    }

    public T? GetParameter<T>(string key)
    {
        var value = GetParameter(key);
        if (value is T typed)
        {
            return typed;
        }
        
        if (value != null)
        {
            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                // Conversion failed, return default
            }
        }
        
        return default(T);
    }

    public void SetParameter(string key, object? value)
    {
        _parameters[key] = value;
    }

    public bool HasParameter(string key)
    {
        return _parameters.ContainsKey(key);
    }

    public IEnumerable<string> GetParameterKeys()
    {
        return _parameters.Keys;
    }
}
