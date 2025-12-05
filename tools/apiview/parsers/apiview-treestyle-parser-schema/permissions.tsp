namespace ApiView;

/** User roles within APIView with increasing permission levels */
enum UserRole {
    /** Service team members who create and manage reviews */
    ServiceTeam,
    /** SDK team members with additional privileges */
    SdkTeam,
    /** Deputy architect with architect-like permissions */
    DeputyArchitect,
    /** Architect with elevated API review permissions */
    Architect,
    /** Admin with full system permissions */
    Admin
}

/** Represents a role assignment that can be global or language-scoped */
model RoleAssignment {
    /** The role being assigned */
    role: UserRole;
    /** 
     * The language this role applies to. 
     * If null or empty, the role is global (applies to all languages).
     */
    language?: string;
}

/** User permissions model stored in Cosmos DB */
model UserPermissions {
    /** Document ID (format: "user-{userId}") */
    id: string;
    /** Document type discriminator */
    type: "user";
    /** GitHub username */
    userId: string;
    /** List of role assignments for this user */
    roles: Array<RoleAssignment>;
    /** Timestamp when permissions were last updated */
    lastUpdatedOn: utcDateTime;
    /** Who last updated these permissions */
    lastUpdatedBy: string;
}

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
}

/** Computed effective permissions (direct + group-inherited) */
model EffectivePermissions {
    /** GitHub username */
    userId: string;
    /** Merged roles from direct assignments and group memberships */
    roles: Array<RoleAssignment>;
}
