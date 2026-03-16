// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

/// <summary>
/// A path string that is always normalized to use forward slashes for cross-platform consistency.
/// Automatically normalizes on construction and provides path manipulation methods that preserve normalization.
/// </summary>
[JsonConverter(typeof(NormalizedPathConverter))]
public readonly struct NormalizedPath(string? path) : IEquatable<NormalizedPath>, IEquatable<string>
{
    private readonly string value = path?.Replace("\\", "/") ?? string.Empty;

    public static string Normalize(string str)
    {
        return new NormalizedPath(str).ToString();
    }

    /// <summary>
    /// Returns true if this path starts with the specified prefix.
    /// </summary>
    public bool StartsWith(string prefix, StringComparison comparison = StringComparison.Ordinal)
        => value.StartsWith(prefix, comparison);

    /// <summary>
    /// Returns true if this path starts with the specified prefix.
    /// </summary>
    public bool StartsWith(NormalizedPath prefix, StringComparison comparison = StringComparison.Ordinal)
        => value.StartsWith(prefix.value, comparison);

    /// <summary>
    /// Returns true if this path ends with the specified suffix.
    /// </summary>
    public bool EndsWith(string suffix, StringComparison comparison = StringComparison.Ordinal)
        => value.EndsWith(suffix, comparison);

    /// <summary>
    /// Returns true if this path contains the specified substring.
    /// </summary>
    public bool Contains(string str, StringComparison comparison = StringComparison.Ordinal)
        => value.Contains(str, comparison);

    /// <summary>
    /// Removes leading characters from this path.
    /// </summary>
    public NormalizedPath TrimStart(params char[] trimChars)
        => new(value.TrimStart(trimChars));

    /// <summary>
    /// Replaces occurrences of a string within this path.
    /// </summary>
    public NormalizedPath Replace(string oldValue, string newValue)
        => new(value.Replace(oldValue, newValue));

    /// <summary>
    /// Returns true if this path is null or empty.
    /// </summary>
    public bool IsEmpty => string.IsNullOrEmpty(value);

    /// <summary>
    /// Gets the length of the path string.
    /// </summary>
    public int Length => value.Length;

    /// <summary>
    /// Returns a substring starting at the specified index.
    /// </summary>
    public string Substring(int startIndex) => value[startIndex..];

    public static implicit operator NormalizedPath(string? path) => new(path);
    public static implicit operator string(NormalizedPath path) => path.value;

    public override string ToString() => value;
    public override int GetHashCode() => value.GetHashCode();
    public override bool Equals(object? obj) => obj switch
    {
        NormalizedPath other => Equals(other),
        string s => Equals(s),
        _ => false
    };
    public bool Equals(NormalizedPath other) => value == other.value;

    public bool Equals(string? other) => value == other?.Replace("\\", "/");

    public static bool operator ==(NormalizedPath left, NormalizedPath right) => left.Equals(right);
    public static bool operator !=(NormalizedPath left, NormalizedPath right) => !left.Equals(right);
}

/// <summary>
/// JSON converter for NormalizedPath that serializes as a plain string.
/// </summary>
public class NormalizedPathConverter : JsonConverter<NormalizedPath>
{
    public override NormalizedPath Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetString());

    public override void Write(Utf8JsonWriter writer, NormalizedPath value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());
}
