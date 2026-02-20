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
    public List<NamespaceDecisionEntry> ApprovedNamespaces { get; set; }
    public List<NamespaceDecisionEntry> NamespaceHistory { get; set; }
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
