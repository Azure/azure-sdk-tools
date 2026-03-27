using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace APIViewWeb.Models;

public readonly record struct PackageKey
{
    public string Language { get; init; }
    public string Flavor { get; init; }
    public PackageKey(string language, string flavor = "")
    {
        Language = language ?? throw new ArgumentNullException(nameof(language));
        Flavor = flavor ?? "";
    }

    public static PackageKey Parse(string value)
    {
        if (string.IsNullOrEmpty(value))
            throw new FormatException("PackageKey cannot be empty.");

        int colon = value.IndexOf(':');
        return colon < 0
            ? new PackageKey(value, "")
            : new PackageKey(value[..colon], value[(colon + 1)..]);
    }

    public bool HasFlavor => !string.IsNullOrEmpty(Flavor);
    public override string ToString() => HasFlavor ? $"{Language}:{Flavor}" : Language;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NamespaceDecisionStatus
{
    Proposed,
    Approved,
    Rejected,
    Withdrawn
}

public class ProjectNamespaceInfo
{
    public List<NamespaceDecisionEntry> ApprovedNamespaces { get; set; } = [];
    // Maps PackageKey string (e.g. "Python", "TypeSpec", "Java", "Java:v2") to the chronological list of namespace decisions.
    public Dictionary<string, List<NamespaceDecisionEntry>> NamespaceHistory { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    // Maps PackageKey string (e.g. "Python", "TypeSpec", "Java", "Java:v2") to the current active namespace decision entry.
    public Dictionary<string, NamespaceDecisionEntry> CurrentNamespaceStatus { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class NamespaceDecisionEntry
{
    public string Language { get; set; }
    public string Flavor { get; set; } = "";
    public string PackageName { get; set; }
    public string Namespace { get; set; }
    public NamespaceDecisionStatus Status { get; set; }
    public string Notes { get; set; }
    public string ProposedBy { get; set; }
    public DateTime? ProposedOn { get; set; }
    public string DecidedBy { get; set; }
    public DateTime? DecidedOn { get; set; }

    [JsonIgnore]
    public PackageKey Key => new(Language, Flavor ?? "");
}

public enum NamespaceOperationError
{
    Unauthorized,
    ProjectNotFound,
    LanguageNotFound,
    InvalidStateTransition
}

#nullable enable
public class NamespaceOperationResult
{
    public Project? Project { get; }
    public NamespaceOperationError? Error { get; }
    public bool IsSuccess => Error == null;

    private NamespaceOperationResult(Project? project, NamespaceOperationError? error)
    {
        Project = project;
        Error = error;
    }

    public static NamespaceOperationResult Success(Project project) => new(project, null);
    public static NamespaceOperationResult Failure(NamespaceOperationError error) => new(null, error);
}
#nullable restore
