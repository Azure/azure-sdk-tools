using System.Text.Json.Serialization;
using System.Collections.Generic;
namespace Azure.Sdk.Tools.Cli.Models;

/// <summary>
/// Cross-language representation of a public (or customization-relevant) API symbol extracted from generated or customization code.
/// This model is intentionally additive; existing simple fields (Id, Kind, Signature, Path) remain for backward compatibility.
/// Future diff logic should rely on stable Id + structured members instead of raw signature text.
/// </summary>
public class SymbolInfo
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty; // Stable logical id (namespace + container + name)
    [JsonPropertyName("kind")] public string Kind { get; set; } = string.Empty; // class, method, function, property, enum, interface, model, client
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty; // Simple name
    [JsonPropertyName("parent")] public string Parent { get; set; } = string.Empty; // Back-compat; prefer ParentId
    [JsonPropertyName("signature")] public string Signature { get; set; } = string.Empty; // Human-readable signature (not stable id)
    [JsonPropertyName("path")] public string Path { get; set; } = string.Empty; // Source file path (relative preferred)

    [JsonPropertyName("namespace")] public string Namespace { get; set; } = string.Empty; // Package / module / namespace
    [JsonPropertyName("parentId")] public string ParentId { get; set; } = string.Empty; // Stable id of containing symbol
    [JsonPropertyName("visibility")] public string Visibility { get; set; } = string.Empty; // public / protected / internal / private (only public normally persisted)
    [JsonPropertyName("language")] public string Language { get; set; } = string.Empty; // e.g. java, python, csharp

    // Method/function specifics
    [JsonPropertyName("returnType")] public string? ReturnType { get; set; }
    [JsonPropertyName("parameters")] public List<ParameterInfo>? Parameters { get; set; }
    [JsonPropertyName("isAsync")] public bool? IsAsync { get; set; }
    [JsonPropertyName("isStatic")] public bool? IsStatic { get; set; }
    [JsonPropertyName("isDeprecated")] public bool? IsDeprecated { get; set; }

    // Model / class specifics
    [JsonPropertyName("properties")] public List<PropertyInfo>? Properties { get; set; }

    // Enum specifics
    [JsonPropertyName("enumMembers")] public List<EnumMemberInfo>? EnumMembers { get; set; }

    // Relationships & mapping
    [JsonPropertyName("relatedGeneratedId")] public string RelatedGeneratedId { get; set; } = string.Empty; // For customization symbol mapping to generated symbol
    [JsonPropertyName("references")] public List<string> References { get; set; } = new(); // Other symbol ids referenced (optional subset)

    // Customization flags
    [JsonPropertyName("isCustomization")] public bool? IsCustomization { get; set; }

    // Structural / diff assist
    [JsonPropertyName("signatureHash")] public string? SignatureHash { get; set; } // Hash of normalized signature for quick diff
    [JsonPropertyName("modifiers")] public List<string>? Modifiers { get; set; } // abstract, final, override, etc.
    [JsonPropertyName("annotations")] public List<string>? Annotations { get; set; } // e.g., @Deprecated, decorators (python)

    // Location for precise edits
    [JsonPropertyName("startLine")] public int? StartLine { get; set; }
    [JsonPropertyName("endLine")] public int? EndLine { get; set; }
}

/// <summary>Parameter within a callable symbol.</summary>
public class ParameterInfo
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
    [JsonPropertyName("kind")] public string Kind { get; set; } = string.Empty; // positional, keyword, vararg, kwvararg, generic, etc.
    [JsonPropertyName("hasDefault")] public bool HasDefault { get; set; }
    [JsonPropertyName("defaultValue")] public string? DefaultValue { get; set; }
    [JsonPropertyName("nullable")] public bool? Nullable { get; set; }
    [JsonPropertyName("raw")] public string? Raw { get; set; } // Raw text fragment if needed
}

/// <summary>Property / field of a model or class.</summary>
public class PropertyInfo
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
    [JsonPropertyName("required")] public bool Required { get; set; }
    [JsonPropertyName("readonly")] public bool ReadOnly { get; set; }
    [JsonPropertyName("nullable")] public bool? Nullable { get; set; }
}

/// <summary>Enumeration member definition.</summary>
public class EnumMemberInfo
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("value")] public string? Value { get; set; } // Underlying value if explicit
}
