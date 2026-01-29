using System.Text.Json.Serialization;

namespace ApiExtractor.Java;

/// <summary>Root container for extracted Java API.</summary>
public class ApiIndex
{
    [JsonPropertyName("package")]
    public string Package { get; set; } = string.Empty;

    [JsonPropertyName("packages")]
    public List<PackageInfo> Packages { get; set; } = [];
}

/// <summary>A Java package containing types.</summary>
public class PackageInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("classes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ClassInfo>? Classes { get; set; }

    [JsonPropertyName("interfaces")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ClassInfo>? Interfaces { get; set; }

    [JsonPropertyName("enums")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<EnumInfo>? Enums { get; set; }
}

/// <summary>A class or interface.</summary>
public class ClassInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("extends")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Extends { get; set; }

    [JsonPropertyName("implements")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Implements { get; set; }

    [JsonPropertyName("doc")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Doc { get; set; }

    [JsonPropertyName("modifiers")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Modifiers { get; set; }

    [JsonPropertyName("typeParams")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TypeParams { get; set; }

    [JsonPropertyName("constructors")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<MethodInfo>? Constructors { get; set; }

    [JsonPropertyName("methods")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<MethodInfo>? Methods { get; set; }

    [JsonPropertyName("fields")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<FieldInfo>? Fields { get; set; }
}

/// <summary>An enum type.</summary>
public class EnumInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("doc")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Doc { get; set; }

    [JsonPropertyName("values")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Values { get; set; }

    [JsonPropertyName("methods")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<MethodInfo>? Methods { get; set; }
}

/// <summary>A method or constructor.</summary>
public class MethodInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("sig")]
    public string Sig { get; set; } = string.Empty;

    [JsonPropertyName("ret")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Ret { get; set; }

    [JsonPropertyName("doc")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Doc { get; set; }

    [JsonPropertyName("modifiers")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Modifiers { get; set; }

    [JsonPropertyName("typeParams")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TypeParams { get; set; }

    [JsonPropertyName("throws")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Throws { get; set; }
}

/// <summary>A field or constant.</summary>
public class FieldInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("doc")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Doc { get; set; }

    [JsonPropertyName("modifiers")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Modifiers { get; set; }

    [JsonPropertyName("value")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Value { get; set; }
}

[JsonSerializable(typeof(ApiIndex))]
[JsonSourceGenerationOptions(WriteIndented = false, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal partial class SourceGenerationContext : JsonSerializerContext { }
