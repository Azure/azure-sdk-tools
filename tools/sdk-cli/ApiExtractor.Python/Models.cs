// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApiExtractor.Python;

// Reuse the same output models for consistency across languages
public record ApiIndex(string Package, List<ModuleInfo> Modules);

public record ModuleInfo(string Name, List<ClassInfo>? Classes, List<FunctionInfo>? Functions);

public record ClassInfo(
    string Name,
    string? Base,
    string? Doc,
    List<MethodInfo>? Methods,
    List<PropertyInfo>? Properties);

public record MethodInfo(
    string Name,
    string Signature,
    string? Doc,
    bool? IsAsync,
    bool? IsClassMethod,
    bool? IsStaticMethod);

public record PropertyInfo(string Name, string? Type, string? Doc);

public record FunctionInfo(
    string Name,
    string Signature,
    string? Doc,
    bool? IsAsync);

[JsonSourceGenerationOptions(
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ApiIndex))]
public partial class ApiIndexContext : JsonSerializerContext { }

public static class ApiIndexExtensions
{
    public static string ToJson(this ApiIndex index) =>
        JsonSerializer.Serialize(index, ApiIndexContext.Default.ApiIndex);
}
