using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace APIViewWeb.LeanModels;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GlobalRole
{
    // Default value
    Unknown = 0,
    ServiceTeam,
    SdkTeam,
    Admin
}


[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LanguageScopedRole
{
    DeputyArchitect = 0,
    Architect
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(GlobalRoleAssignment), "global")]
[JsonDerivedType(typeof(LanguageScopedRoleAssignment), "scoped")]
public abstract class RoleAssignment
{
}

public class GlobalRoleAssignment : RoleAssignment
{
    [JsonPropertyName("role")]
    public GlobalRole Role { get; set; }
}

public class LanguageScopedRoleAssignment : RoleAssignment
{
    [JsonPropertyName("role")]
    public LanguageScopedRole Role { get; set; }

    [JsonPropertyName("language")]
    public string Language { get; set; }
}

public static class RoleAssignmentExtensions
{
    public static bool HasGlobalRole(this IEnumerable<RoleAssignment> roles, GlobalRole role) =>
        roles.Any(r => r is GlobalRoleAssignment gra && gra.Role == role);

    public static bool HasAnyLanguageRole(this IEnumerable<RoleAssignment> roles, string language, params LanguageScopedRole[] targetRoles) =>
        roles.Any(r => r is LanguageScopedRoleAssignment lra &&
            targetRoles.Contains(lra.Role) &&
            string.Equals(lra.Language, language, StringComparison.OrdinalIgnoreCase));

    public static bool GrantsApprovalFor(this IEnumerable<RoleAssignment> roles, string language) =>
        roles.HasGlobalRole(GlobalRole.Admin) ||
        roles.HasAnyLanguageRole(language, LanguageScopedRole.Architect, LanguageScopedRole.DeputyArchitect);

    public static bool GrantsApprovalForAnyLanguage(this IEnumerable<RoleAssignment> roles) =>
        roles.HasGlobalRole(GlobalRole.Admin) ||
        roles.Any(r => r is LanguageScopedRoleAssignment { Role: LanguageScopedRole.Architect or LanguageScopedRole.DeputyArchitect });
}

public class GroupPermissionsModel
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("type")]
    public string Type => "group";

    [JsonPropertyName("groupId")]
    public string GroupId { get; set; }

    [JsonPropertyName("groupName")]
    public string GroupName { get; set; }

    [JsonPropertyName("roles")]
    public List<RoleAssignment> Roles { get; set; } = new();

    [JsonPropertyName("members")]
    public List<string> Members { get; set; } = new();

    [JsonPropertyName("lastUpdatedOn")]
    public DateTime LastUpdatedOn { get; set; }

    [JsonPropertyName("lastUpdatedBy")]
    public string LastUpdatedBy { get; set; }

    [JsonPropertyName("serviceNames")]
    public List<string> ServiceNames { get; set; } = new();
}

public class EffectivePermissions
{
    [JsonPropertyName("userId")]
    public string UserId { get; set; }

    [JsonPropertyName("roles")]
    public List<RoleAssignment> Roles { get; set; } = new();

    public bool IsAdmin => Roles.HasGlobalRole(GlobalRole.Admin);
    public bool IsLanguageApprover => Roles.GrantsApprovalForAnyLanguage();
    public bool IsApproverFor(string language) => Roles.GrantsApprovalFor(language);
}

public class GroupPermissionsRequest
{
    [JsonPropertyName("groupId")]
    public string GroupId { get; set; }

    [JsonPropertyName("groupName")]
    public string GroupName { get; set; }

    [JsonPropertyName("roles")]
    public List<RoleAssignment> Roles { get; set; } = [];

    [JsonPropertyName("serviceNames")]
    public List<string> ServiceNames { get; set; } = [];
}

public class AddMembersRequest
{
    [JsonPropertyName("userIds")]
    public List<string> UserIds { get; set; } = [];
}

public class AddMembersResult
{
    [JsonPropertyName("addedUsers")]
    public List<string> AddedUsers { get; set; } = [];

    [JsonPropertyName("invalidUsers")]
    public List<string> InvalidUsers { get; set; } = [];

    [JsonPropertyName("allUsersValid")]
    public bool AllUsersValid => InvalidUsers.Count == 0;
}
