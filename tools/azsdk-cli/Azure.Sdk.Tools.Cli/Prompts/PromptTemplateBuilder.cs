// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Prompts;

/// <summary>
/// Builder for creating prompt contexts in a fluent manner.
/// </summary>
public class PromptContextBuilder
{
    private readonly PromptContext _context = new();

    /// <summary>
    /// Adds a parameter to the context.
    /// </summary>
    /// <param name="key">The parameter key</param>
    /// <param name="value">The parameter value</param>
    /// <returns>The builder instance for chaining</returns>
    public PromptContextBuilder WithParameter(string key, object? value)
    {
        _context.SetParameter(key, value);
        return this;
    }

    /// <summary>
    /// Builds the prompt context.
    /// </summary>
    /// <returns>The constructed prompt context</returns>
    public IPromptContext Build()
    {
        return _context;
    }

    /// <summary>
    /// Creates a new builder instance.
    /// </summary>
    /// <returns>A new PromptContextBuilder</returns>
    public static PromptContextBuilder Create()
    {
        return new PromptContextBuilder();
    }
}

/// <summary>
/// Registry for managing and retrieving prompt templates.
/// </summary>
public class PromptTemplateRegistry
{
    private readonly Dictionary<string, IPromptTemplate> _templates = new();
    private static readonly Lazy<PromptTemplateRegistry> _instance = new(() => new PromptTemplateRegistry());

    /// <summary>
    /// Gets the singleton instance of the registry.
    /// </summary>
    public static PromptTemplateRegistry Instance => _instance.Value;

    /// <summary>
    /// Registers a prompt template in the registry.
    /// </summary>
    /// <param name="template">The template to register</param>
    /// <exception cref="ArgumentException">Thrown if a template with the same ID is already registered</exception>
    public void RegisterTemplate(IPromptTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        
        if (_templates.ContainsKey(template.TemplateId))
        {
            throw new ArgumentException($"Template with ID '{template.TemplateId}' is already registered");
        }

        _templates[template.TemplateId] = template;
    }

    /// <summary>
    /// Gets a template by its ID.
    /// </summary>
    /// <param name="templateId">The template ID</param>
    /// <returns>The template instance</returns>
    /// <exception cref="KeyNotFoundException">Thrown if the template is not found</exception>
    public IPromptTemplate GetTemplate(string templateId)
    {
        if (_templates.TryGetValue(templateId, out var template))
        {
            return template;
        }

        throw new KeyNotFoundException($"Template with ID '{templateId}' not found");
    }

    /// <summary>
    /// Tries to get a template by its ID.
    /// </summary>
    /// <param name="templateId">The template ID</param>
    /// <param name="template">The template instance if found</param>
    /// <returns>True if the template was found, false otherwise</returns>
    public bool TryGetTemplate(string templateId, out IPromptTemplate? template)
    {
        return _templates.TryGetValue(templateId, out template);
    }
}

/// <summary>
/// Utility class for working with prompt templates.
/// </summary>
public static class PromptTemplateHelper
{
    /// <summary>
    /// Builds a prompt using a template from the registry.
    /// </summary>
    /// <param name="templateId">The template ID</param>
    /// <param name="context">The prompt context</param>
    /// <returns>The built prompt string</returns>
    public static string BuildPrompt(string templateId, IPromptContext context)
    {
        var template = PromptTemplateRegistry.Instance.GetTemplate(templateId);
        return template.BuildPrompt(context);
    }

    /// <summary>
    /// Builds a prompt using a template from the registry with a fluent context builder.
    /// </summary>
    /// <param name="templateId">The template ID</param>
    /// <param name="contextBuilder">Action to build the context</param>
    /// <returns>The built prompt string</returns>
    public static string BuildPrompt(string templateId, Action<PromptContextBuilder> contextBuilder)
    {
        var builder = PromptContextBuilder.Create();
        contextBuilder(builder);
        var context = builder.Build();
        
        return BuildPrompt(templateId, context);
    }
}
