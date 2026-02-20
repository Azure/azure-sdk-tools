using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using APIViewWeb.LeanModels;

namespace APIViewWeb.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProjectChangeAction
{
    Created,
    Edited,
    Deleted,
    UnDeleted,
    ReviewLinked
}

public class Project
{
    [JsonPropertyName("id")]
    public string Id { get; set; }
    public string CrossLanguagePackageId { get; set; }
    public string DisplayName { get; set; }
    public string Description { get; set; }
    public List<string> Owners { get; set; }
    public string Namespace { get; set; }
    public Dictionary<string, PackageInfo> ExpectedPackages { get; set; }
    public ProjectNamespaceInfo NamespaceInfo { get; set; }
    public List<ProjectChangeHistory> ChangeHistory { get; set; }
    public HashSet<string> ReviewIds { get; set; }
    public DateTime CreatedOn { get; set; }
    public DateTime LastUpdatedOn { get; set; }
    public bool IsDeleted { get; set; }
}

public class ProjectChangeHistory : ChangeHistoryModel
{
    public ProjectChangeAction ChangeAction { get; set; }
}

public class PackageInfo
{
    public string PackageName { get; set; }
    public string Namespace { get; set; }
}
