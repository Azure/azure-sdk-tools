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

The following roles are proposed, ordered from least to most permissive:

| Role | Description | Scope |
|------|-------------|-------|
| **ServiceTeam** | Service team members who create and manage reviews | Not language-specific |
| **SdkTeam** | SDK team members with additional privileges | Not language-specific |
| **DeputyArchitect** | Similar permissions to Architect, for designated backups | Per-language |
| **Architect** | Elevated permissions for API review approval | Per-language |
| **Admin** | Highest level permissions, for APIView maintainers only | Not language-specific |

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

Create a new `Permissions` container in Cosmos DB:

**User Permission Document:**
```json
{
  "id": "user-almend",
  "type": "user",
  "userId": "almend",
  "roles": [
    { "role": "SdkTeam", "language": null },
    { "role": "Architect", "language": "Python" }
  ],
  "lastUpdatedOn": "2024-12-04T00:00:00Z",
  "lastUpdatedBy": "admin"
}
```

**Group Permission Document:**
```json
{
  "id": "group-python-architects",
  "type": "group",
  "groupId": "python-architects",
  "groupName": "Python Architects",
  "roles": [
    { "role": "Architect", "language": "Python" }
  ],
  "members": ["almend", "johanste"],
  "lastUpdatedOn": "2024-12-04T00:00:00Z",
  "lastUpdatedBy": "admin"
}
```
---

## 5. Effective Permissions Resolution

The key to this system is computing a user's **effective permissions** by merging their direct role assignments with roles inherited from group memberships.

```csharp
public async Task<EffectivePermissions> GetEffectivePermissionsAsync(string userId)
{
    // 1. Get direct user permissions
    var userPerms = await _permissionRepo.GetUserPermissionsAsync(userId);
    
    // 2. Get all groups where user is a member
    var groups = await _permissionRepo.GetGroupsForUserAsync(userId);
    
    // 3. Merge roles (direct + group-inherited), highest role wins per language
    return MergePermissions(userPerms, groups);
}
```

**Example**: User has direct `SdkTeam` (global) + group membership gives `Architect` (Python)
- `HasRole(Architect, "Python")` → ✅ true
- `HasRole(Architect, "Java")` → ❌ false  
- `HasRole(SdkTeam, "Java")` → ✅ true (global applies)

---

## 6. API Summary Table

| Method | Endpoint | Description | Required Role |
|--------|----------|-------------|---------------|
| GET | `/api/permissions/me` | Get current user's permissions | Authenticated |
| GET | `/api/permissions/users/{userId}` | Get user's permissions | Admin |
| PUT | `/api/permissions/users/{userId}/roles` | Assign role to user | Admin |
| DELETE | `/api/permissions/users/{userId}/roles/{role}` | Remove role from user | Admin |
| GET | `/api/permissions/groups` | List all groups | Admin |
| GET | `/api/permissions/groups/{groupId}` | Get group details | Admin |
| POST | `/api/permissions/groups` | Create group | Admin |
| PUT | `/api/permissions/groups/{groupId}` | Update group | Admin |
| DELETE | `/api/permissions/groups/{groupId}` | Delete group | Admin |
| POST | `/api/permissions/groups/{groupId}/members` | Add members to group | Admin |
| DELETE | `/api/permissions/groups/{groupId}/members/{userId}` | Remove member from group | Admin |

---

## 7. Related Issues

- [[APIView] Improve the concept of "Approvers" #8784](https://github.com/Azure/azure-sdk-tools/issues/8784)

---
