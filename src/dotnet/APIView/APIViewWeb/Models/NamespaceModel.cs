using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace APIViewWeb.Models;

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
    // Maps language name (e.g. "Python", "TypeSpec") to the chronological list of namespace decisions for that language.
    public Dictionary<string, List<NamespaceDecisionEntry>> NamespaceHistory { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    // Maps language name (e.g. "Python", "TypeSpec") to the current active namespace decision entry.
    public Dictionary<string, NamespaceDecisionEntry> CurrentNamespaceStatus { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class NamespaceDecisionEntry
{
    public string Language { get; set; }
    public string PackageName { get; set; }
    public string Namespace { get; set; }
    public NamespaceDecisionStatus Status { get; set; }
    public string Notes { get; set; }
    public string ProposedBy { get; set; }
    public DateTime? ProposedOn { get; set; }
    public string DecidedBy { get; set; }
    public DateTime? DecidedOn { get; set; }
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
