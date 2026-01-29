// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApiExtractor.DotNet;

/// <summary>
/// Output model for extracted API surface.
/// Minimal schema - only what AI needs to understand the SDK.
/// </summary>
public record ApiIndex
{
    [JsonPropertyName("package")]
    public string Package { get; init; } = "";
    
    [JsonPropertyName("version")]
    public string? Version { get; init; }
    
    [JsonPropertyName("namespaces")]
    public List<NamespaceInfo> Namespaces { get; init; } = [];
    
    public string ToJson() => JsonSerializer.Serialize(this, JsonContext.Default.ApiIndex);
}

public record NamespaceInfo
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";
    
    [JsonPropertyName("types")]
    public List<TypeInfo> Types { get; init; } = [];
}

public record TypeInfo
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";
    
    [JsonPropertyName("kind")]
    public string Kind { get; init; } = ""; // class, interface, struct, enum, record, delegate
    
    [JsonPropertyName("base")]
    public string? Base { get; init; }
    
    [JsonPropertyName("interfaces")]
    public List<string>? Interfaces { get; init; }
    
    [JsonPropertyName("doc")]
    public string? Doc { get; init; }
    
    [JsonPropertyName("members")]
    public List<MemberInfo>? Members { get; init; }
    
    [JsonPropertyName("values")]
    public List<string>? Values { get; init; } // For enums
}

public record MemberInfo
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";
    
    [JsonPropertyName("kind")]
    public string Kind { get; init; } = ""; // ctor, method, property, field, event, indexer
    
    [JsonPropertyName("sig")]
    public string Signature { get; init; } = ""; // Compressed signature
    
    [JsonPropertyName("doc")]
    public string? Doc { get; init; }
    
    [JsonPropertyName("static")]
    public bool? IsStatic { get; init; }
    
    [JsonPropertyName("async")]
    public bool? IsAsync { get; init; }
}

[JsonSerializable(typeof(ApiIndex))]
[JsonSourceGenerationOptions(
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class JsonContext : JsonSerializerContext { }
