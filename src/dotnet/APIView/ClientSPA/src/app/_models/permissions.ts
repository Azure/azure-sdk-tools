/**
 * Global roles that apply to all languages
 * Note: Values must be camelCase to match backend JSON serialization
 */
export enum GlobalRole {
    Unknown = 'unknown',
    ServiceTeam = 'serviceTeam',
    SdkTeam = 'sdkTeam',
    Admin = 'admin'
}

/**
 * Language-scoped roles that must be assigned to a specific language
 * Note: Values must be camelCase to match backend JSON serialization
 */
export enum LanguageScopedRole {
    DeputyArchitect = 'deputyArchitect',
    Architect = 'architect'
}

/**
 * Base interface for role assignments (discriminated union)
 */
export interface GlobalRoleAssignment {
    kind: 'global';
    role: GlobalRole;
}

export interface LanguageScopedRoleAssignment {
    kind: 'scoped';
    role: LanguageScopedRole;
    language: string;
}

/**
 * A role assignment can be either global or language-scoped
 */
export type RoleAssignment = GlobalRoleAssignment | LanguageScopedRoleAssignment;

/**
 * Group-based permission assignment stored in Cosmos DB
 */
export interface GroupPermissions {
    id: string;
    type: 'group';
    groupId: string;
    groupName: string;
    roles: RoleAssignment[];
    members: string[];
    lastUpdatedOn: string;
    lastUpdatedBy: string;
    serviceNames: string[];
}

/**
 * Computed effective permissions (direct + group-inherited)
 */
export interface EffectivePermissions {
    userId: string;
    roles: RoleAssignment[];
}

/**
 * Request model for creating or updating a group
 */
export interface GroupPermissionsRequest {
    groupId: string;
    groupName: string;
    roles: RoleAssignment[];
    serviceNames: string[];
}

/**
 * Request model for adding members to a group
 */
export interface AddMembersRequest {
    userIds: string[];
}

/**
 * Result model for adding members to a group
 */
export interface AddMembersResult {
    addedUsers: string[];
    invalidUsers: string[];
    allUsersValid: boolean;
}

export const ROLE_DISPLAY_NAMES: { [key: string]: string } = {
    [GlobalRole.Admin]: 'Admin',
    [GlobalRole.ServiceTeam]: 'Service Team',
    [GlobalRole.SdkTeam]: 'SDK Team',
    [GlobalRole.Unknown]: 'Unknown',
    [LanguageScopedRole.Architect]: 'Architect',
    [LanguageScopedRole.DeputyArchitect]: 'Deputy Architect'
};

export const SUPPORTED_LANGUAGES = [
    { label: 'C', value: 'C' },
    { label: 'C++', value: 'C++' },
    { label: 'C#', value: 'C#' },
    { label: 'Go', value: 'Go' },
    { label: 'Java', value: 'Java' },
    { label: 'JavaScript', value: 'JavaScript' },
    { label: 'Python', value: 'Python' },
    { label: 'Swift', value: 'Swift' },
    { label: 'TypeSpec', value: 'TypeSpec' }
];

export const GLOBAL_ROLE_OPTIONS = [
    { label: ROLE_DISPLAY_NAMES[GlobalRole.ServiceTeam], value: GlobalRole.ServiceTeam },
    { label: ROLE_DISPLAY_NAMES[GlobalRole.SdkTeam], value: GlobalRole.SdkTeam },
    { label: ROLE_DISPLAY_NAMES[GlobalRole.Admin], value: GlobalRole.Admin }
];

export const LANGUAGE_SCOPED_ROLE_OPTIONS = [
    { label: ROLE_DISPLAY_NAMES[LanguageScopedRole.DeputyArchitect], value: LanguageScopedRole.DeputyArchitect },
    { label: ROLE_DISPLAY_NAMES[LanguageScopedRole.Architect], value: LanguageScopedRole.Architect }
];

export function formatRoleName(role: string): string {
    return ROLE_DISPLAY_NAMES[role] || role;
}
