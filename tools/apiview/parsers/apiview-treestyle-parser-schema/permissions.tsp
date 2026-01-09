namespace ApiView;

/** Roles that apply globally to all languages */
enum GlobalRole {
    /** Default value */
    Unknown,
    /** Service team members who create and manage reviews */
    ServiceTeam,
    /** SDK team members with additional privileges */
    SdkTeam,
    /** Admin with full system permissions */
    Admin
}

/** Roles that must be scoped to a specific language */
enum LanguageScopedRole {
    /** Deputy architect with architect-like permissions for a specific language */
    DeputyArchitect,
    /** Architect with elevated API review permissions for a specific language */
    Architect
}

/** Assignment for global roles - applies to all languages */
model GlobalRoleAssignment {
    /** Discriminator for role assignment type */
    kind: "global";
    /** The global role being assigned */
    role: GlobalRole;
}

/** Assignment for language-scoped roles - requires a specific language */
model LanguageScopedRoleAssignment {
    /** Discriminator for role assignment type */
    kind: "scoped";
    /** The language-scoped role being assigned */
    role: LanguageScopedRole;
    /** The specific language this role applies to (required) */
    language: string;
}

/** A role assignment can be either global or language-scoped */
alias RoleAssignment = GlobalRoleAssignment | LanguageScopedRoleAssignment;

/** Group-based permission assignment stored in Cosmos DB */
model GroupPermissions {
    /** Document ID (format: "group-{groupId}") */
    id: string;
    /** Document type discriminator */
    type: "group";
    /** Unique identifier for the group */
    groupId: string;
    /** Display name for the group */
    groupName: string;
    /** List of role assignments for this group */
    roles: Array<RoleAssignment>;
    /** List of user IDs that belong to this group */
    members: Array<string>;
    /** Timestamp when group was last updated */
    lastUpdatedOn: utcDateTime;
    /** Who last updated this group */
    lastUpdatedBy: string;
    /** List of service names associated with this group */
    serviceNames: Array<string>;
}

/** Computed effective permissions (direct + group-inherited) */
model EffectivePermissions {
    /** GitHub username */
    userId: string;
    /** Merged roles from direct assignments and group memberships */
    roles: Array<RoleAssignment>;
}
