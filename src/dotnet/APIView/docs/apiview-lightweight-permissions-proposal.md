# Proposal: APIView Lightweight App Permissions 
[Azure/azure-sdk-tools#13004](https://github.com/Azure/azure-sdk-tools/issues/13004)

---

## 1. Problem

Currently, APIView has a simple permissions model based on a configuration-level "approvers" list (`configuration["approvers"]`). This approach has several limitations:

1. **No formal role hierarchy** - Anyone in the approvers list gets the same elevated permissions
2. **No language scoping** - Users can't be designated as architects for specific languages
3. **Sloppy modeling** - The current system treats anyone in the approvers list as an architect
4. **Limited UI control** - No clear mechanism to show/hide features based on user roles
5. **Self-select nature** - The approvers list is self-administered

---

## 2. Proposed User Roles

Roles are divided into two categories based on their scope:

### Global Roles
These roles apply to all languages and cannot be language-scoped:

| Role | Description |
|------|-------------|
| **ServiceTeam** | Service team members who create and manage reviews |
| **SdkTeam** | SDK team members with additional privileges |
| **Admin** | Highest level permissions, for APIView maintainers only |

### Language-Scoped Roles
These roles **must** be assigned to a specific language:

| Role | Description |
|------|-------------|
| **DeputyArchitect** | Similar permissions to Architect, for designated backups |
| **Architect** | Elevated permissions for API review approval |

---

## 3. Permission Matrix

| Action | ServiceTeam | SdkTeam | DeputyArchitect | Architect | Admin |
|--------|:-----------:|:-------:|:---------------:|:---------:|:-----:|
| View reviews | ✓ | ✓ | ✓ | ✓ | ✓ |
| Create reviews | ✓ | ✓ | ✓ | ✓ | ✓ |
| Add comments | ✓ | ✓ | ✓ | ✓ | ✓ |
| Delete own comments | ✓ | ✓ | ✓ | ✓ | ✓ |
| Delete any comment | | | ✓ | ✓ | ✓ |
| Delete all copilot comments | | | | | ✓ |
| Approve API revision | | | ✓* | ✓* | ✓ |
| Approve namespace | | | ✓* | ✓* | ✓ |
| Delete revision | ✓ | ✓ | ✓ | ✓ | ✓ |
| Delete entire review | | | | | ✓ |
| Manage user permissions | | | | | ✓ |
| Access admin features | | | | | ✓ |

*\* Language-scoped: Only for languages where user has this role*

---

## 4. Data Storage

Create a new `Permissions` container in Cosmos DB.

### Role Assignment Schema (Discriminated Union)

Role assignments use a discriminated union pattern with a `kind` field to distinguish between global and language-scoped roles. This design **enforces at the schema level** that:
- Global roles (`Admin`, `SdkTeam`, `ServiceTeam`) cannot have a language
- Language-scoped roles (`Architect`, `DeputyArchitect`) must have a language

**Global Role Assignment:**
```json
{ "kind": "global", "role": "SdkTeam" }
```

**Language-Scoped Role Assignment:**
```json
{ "kind": "scoped", "role": "Architect", "language": "Python" }
```

### Group Permission Document
```json
{
  "id": "group-python-architects",
  "type": "group",
  "groupId": "python-architects",
  "groupName": "Python Architects",
  "roles": [
    { "kind": "scoped", "role": "Architect", "language": "Python" }
  ],
  "members": ["almend", "johanste"],
  "lastUpdatedOn": "2024-12-04T00:00:00Z",
  "lastUpdatedBy": "admin"
}
```
---

## 5. Effective Permissions Resolution

The key to this system is computing a user's **effective permissions** by merging roles inherited from all their group memberships.

```csharp
public async Task<EffectivePermissions> GetEffectivePermissionsAsync(string userId)
{
    // 1. Get all groups where user is a member
    var groups = await _permissionRepo.GetGroupsForUserAsync(userId);
    
    // 2. Merge roles from all groups, highest role wins per language
    return MergePermissions(groups);
}
```

**Example**: group membership  "Sdk team" gives `SdkTeam` (global) + group membership "Python Architects" gives `Architect` (Python)
- `HasRole(Architect, "Python")` → ✅ true
- `HasRole(Architect, "Java")` → ❌ false  
- `HasRole(SdkTeam, "Java")` → ✅ true (global applies)

---

## 6. UI-Backend Communication

### Overview

The user's effective permissions will be returned as part of the existing `UserProfile` response. This approach:
- **Minimizes API calls** - No separate request needed for permissions
- **Ensures consistency** - Permissions are always in sync with the user profile
- **Simplifies caching** - Single source of truth for user data

### Backend Changes

Extend `UserProfileModel` to include the computed effective permissions:

```csharp
public class UserProfileModel
{
    [JsonPropertyName("id")]
    public string UserName { get; set; }
    
    [JsonPropertyName("userName")]
    public string UserNameAlias => UserName;
    
    public string Email { get; set; }
    public UserPreferenceModel Preferences { get; set; }
    
    // New: Effective permissions (computed from group memberships)
    public EffectivePermissions Permissions { get; set; }
}
```

The `GET /api/userprofile` endpoint will compute and return the user's effective permissions:

```csharp
[HttpGet]
public async Task<ActionResult<UserProfileModel>> GetUserProfile([FromQuery] string userName = null)
{
    userName = userName ?? User.GetGitHubLogin();
    var userProfile = await _userProfileCache.GetUserProfileAsync(userName);
    
    // Compute effective permissions from group memberships
    userProfile.Permissions = await _permissionsManager.GetEffectivePermissionsAsync(userName);
    
    return new LeanJsonResult(userProfile, StatusCodes.Status200OK);
}
```

Define the permission types:

```typescript
// Global roles (no language scope)
export type GlobalRole = "Unknown" | "ServiceTeam" | "SdkTeam" | "Admin";

// Language-scoped roles (require language)
export type LanguageScopedRole = "DeputyArchitect" | "Architect";

// Discriminated union for role assignments
export type RoleAssignment = 
    | { kind: "global"; role: GlobalRole }
    | { kind: "scoped"; role: LanguageScopedRole; language: string };

export interface EffectivePermissions {
    userId: string;
    roles: RoleAssignment[];
}
```

### Permission Helper Service

Create a service to check permissions in the UI. Methods accept arrays of roles to allow checking multiple roles in a single call:

```typescript
@Injectable({ providedIn: 'root' })
export class PermissionsService {
    
    /** Check if user has any of the specified global roles */
    hasGlobalRole(permissions: EffectivePermissions, roles: GlobalRole | GlobalRole[]): boolean {
        const roleArray = Array.isArray(roles) ? roles : [roles];
        return permissions.roles.some(r => 
            r.kind === "global" && roleArray.includes(r.role)
        );
    }
    
    /** Check if user has any of the specified language-scoped roles for a specific language */
    hasLanguageRole(permissions: EffectivePermissions, roles: LanguageScopedRole | LanguageScopedRole[], language: string): boolean {
        const roleArray = Array.isArray(roles) ? roles : [roles];
        return permissions.roles.some(r => 
            r.kind === "scoped" && roleArray.includes(r.role) && r.language === language
        );
    }
    
    /** Check if user can approve for a specific language (Architect, DeputyArchitect, or Admin) */
    canApprove(permissions: EffectivePermissions, language: string): boolean {
        return this.hasGlobalRole(permissions, "Admin") ||
               this.hasLanguageRole(permissions, ["Architect", "DeputyArchitect"], language);
    }
    
    /** Check if user has elevated global permissions (SdkTeam or Admin) */
    hasElevatedAccess(permissions: EffectivePermissions): boolean {
        return this.hasGlobalRole(permissions, ["Admin"]);
    }
}
```

**Usage examples:**
```typescript
// Check for a single role
this.permissionsService.hasGlobalRole(permissions, "SdkTeam");

// Check for multiple roles at once
this.permissionsService.hasGlobalRole(permissions, ["SdkTeam", "Admin"]);

// Check language-scoped roles
this.permissionsService.hasLanguageRole(permissions, ["Architect", "DeputyArchitect"], "Python");
```

### Example Response

`GET /api/userprofile` response:

```json
{
  "userName": "almend",
  "email": "almend@microsoft.com",
  "preferences": { ... },
  "permissions": {
    "userId": "almend",
    "roles": [
      { "kind": "global", "role": "SdkTeam" },
      { "kind": "scoped", "role": "Architect", "language": "Python" },
      { "kind": "scoped", "role": "DeputyArchitect", "language": "Java" }
    ]
  }
}
```

### UI Usage Example

```typescript
// In a component
if (this.permissionsService.canApprove(this.userProfile.permissions, review.language)) {
    this.showApproveButton = true;
}

// Check for admin-only features
if (this.permissionsService.hasGlobalRole(this.userProfile.permissions, "Admin")) {
    this.showAdminPanel = true;
}

// Check for elevated access (SdkTeam or Admin)
if (this.permissionsService.hasElevatedAccess(this.userProfile.permissions)) {
    this.showAdvancedOptions = true;
}
```

---

## 7. API Summary Table

| Method | Endpoint | Description | Required Role |
|--------|----------|-------------|---------------|
| GET | `/api/permissions/me` | Get current user's permissions | Authenticated |
| GET | `/api/permissions/groups` | List all groups | Admin |
| GET | `/api/permissions/groups/{groupId}` | Get group details | Admin |
| POST | `/api/permissions/groups` | Create group | Admin |
| PUT | `/api/permissions/groups/{groupId}` | Update group | Admin |
| DELETE | `/api/permissions/groups/{groupId}` | Delete group | Admin |
| POST | `/api/permissions/groups/{groupId}/members` | Add members to group | Admin |
| DELETE | `/api/permissions/groups/{groupId}/members/{userId}` | Remove member from group | Admin |

---

## 8. Related Issues

- [[APIView] Improve the concept of "Approvers" #8784](https://github.com/Azure/azure-sdk-tools/issues/8784)

---
