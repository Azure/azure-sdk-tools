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

    public bool IsAdmin => HasGlobalRole(GlobalRole.Admin);

    public bool HasElevatedAccess => HasAnyGlobalRole(GlobalRole.SdkTeam, GlobalRole.Admin);

    public bool HasGlobalRole(GlobalRole role)
    {
        return Roles.Exists(r => r is GlobalRoleAssignment gra && gra.Role == role);
    }
    public bool HasAnyGlobalRole(params GlobalRole[] roles)
    {
        foreach (var role in roles)
        {
            if (HasGlobalRole(role))
            {
                return true;
            }
        }

        return false;
    }

    public bool HasLanguageRole(LanguageScopedRole role, string language)
    {
        return Roles.Exists(r =>
            r is LanguageScopedRoleAssignment lra &&
            lra.Role == role &&
            string.Equals(lra.Language, language, StringComparison.OrdinalIgnoreCase));
    }

    public bool HasAnyLanguageRole(string language, params LanguageScopedRole[] roles)
    {
        return roles.Any(role => HasLanguageRole(role, language));
    }

    public bool CanApprove(string language)
    {
        return HasGlobalRole(GlobalRole.Admin) ||
               HasAnyLanguageRole(language, LanguageScopedRole.Architect, LanguageScopedRole.DeputyArchitect);
    }
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
