import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { HttpClient } from '@angular/common/http';
import { ConfigService } from '../config/config.service';
import { 
    EffectivePermissions, 
    GlobalRole, 
    LanguageScopedRole, 
    GroupPermissions,
    GroupPermissionsRequest,
    AddMembersRequest,
    AddMembersResult
} from 'src/app/_models/permissions';

@Injectable({
    providedIn: 'root'
})
export class PermissionsService {
    private baseUrl: string = this.configService.apiUrl + 'permissions';

    constructor(
        private http: HttpClient,
        private configService: ConfigService
    ) { }

    getMyPermissions(): Observable<EffectivePermissions> {
        return this.http.get<EffectivePermissions>(`${this.baseUrl}/me`, { withCredentials: true });
    }

    /**
     * Get all permission groups (Admin only)
     */
    getAllGroups(): Observable<GroupPermissions[]> {
        return this.http.get<GroupPermissions[]>(`${this.baseUrl}/groups`, { withCredentials: true });
    }

    /**
     * Get a specific group by ID (Admin only)
     */
    getGroup(groupId: string): Observable<GroupPermissions> {
        return this.http.get<GroupPermissions>(`${this.baseUrl}/groups/${groupId}`, { withCredentials: true });
    }

    /**
     * Create a new permission group (Admin only)
     */
    createGroup(request: GroupPermissionsRequest): Observable<GroupPermissions> {
        return this.http.post<GroupPermissions>(`${this.baseUrl}/groups`, request, { withCredentials: true });
    }

    /**
     * Update an existing permission group (Admin only)
     */
    updateGroup(groupId: string, request: GroupPermissionsRequest): Observable<GroupPermissions> {
        return this.http.put<GroupPermissions>(`${this.baseUrl}/groups/${groupId}`, request, { withCredentials: true });
    }

    /**
     * Delete a permission group (Admin only)
     */
    deleteGroup(groupId: string): Observable<void> {
        return this.http.delete<void>(`${this.baseUrl}/groups/${groupId}`, { withCredentials: true });
    }

    /**
     * Add members to a group (Admin only)
     */
    addMembersToGroup(groupId: string, userIds: string[]): Observable<AddMembersResult> {
        const request: AddMembersRequest = { userIds };
        return this.http.post<AddMembersResult>(`${this.baseUrl}/groups/${groupId}/members`, request, { withCredentials: true });
    }

    /**
     * Remove a member from a group (Admin only)
     */
    removeMemberFromGroup(groupId: string, userId: string): Observable<void> {
        return this.http.delete<void>(`${this.baseUrl}/groups/${groupId}/members/${userId}`, { withCredentials: true });
    }

    /**
     * Get all usernames for autocomplete (Admin only)
     * This loads all users once - filtering is done client-side
     */
    getAllUsernames(): Observable<string[]> {
        return this.http.get<string[]>(`${this.baseUrl}/users`, { withCredentials: true });
    }

    /**
     * Get all approvers for a specific language
     */
    getApproversForLanguage(language: string): Observable<string[]> {
        return this.http.get<string[]>(`${this.baseUrl}/approvers/${encodeURIComponent(language)}`, { withCredentials: true });
    }

    /**
     * Get the groups that the current user belongs to
     */
    getMyGroups(): Observable<GroupPermissions[]> {
        return this.http.get<GroupPermissions[]>(`${this.baseUrl}/me/groups`, { withCredentials: true });
    }

    /**
     * Get the list of admin usernames for contact information
     */
    getAdminUsernames(): Observable<string[]> {
        return this.http.get<string[]>(`${this.baseUrl}/admins`, { withCredentials: true });
    }

    hasGlobalRole(permissions: EffectivePermissions | null | undefined, roles: GlobalRole | GlobalRole[]): boolean {
        if (!permissions || !permissions.roles) {
            return false;
        }
        const roleArray = Array.isArray(roles) ? roles : [roles];
        return permissions.roles.some(r =>
            r.kind === 'global' && roleArray.includes(r.role as GlobalRole)
        );
    }

    hasLanguageRole(
        permissions: EffectivePermissions | null | undefined, 
        roles: LanguageScopedRole | LanguageScopedRole[], 
        language: string
    ): boolean {
        if (!permissions || !permissions.roles) {
            return false;
        }
        const roleArray = Array.isArray(roles) ? roles : [roles];
        return permissions.roles.some(r =>
            r.kind === 'scoped' && 
            roleArray.includes(r.role as LanguageScopedRole) && 
            r.language.toLowerCase() === language.toLowerCase()
        );
    }

    isApproverFor(permissions: EffectivePermissions | null | undefined, language: string | null | undefined): boolean {
        if (!language) {
            return false;
        }
        return this.hasGlobalRole(permissions, GlobalRole.Admin) ||
            this.hasLanguageRole(permissions, [LanguageScopedRole.Architect, LanguageScopedRole.DeputyArchitect], language);
    }

    isAdmin(permissions: EffectivePermissions | null | undefined): boolean {
        return this.hasGlobalRole(permissions, GlobalRole.Admin);
    }

    isLanguageApprover(permissions: EffectivePermissions | null | undefined): boolean {
        if (!permissions || !permissions.roles) {
            return false;
        }
        
        if (this.isAdmin(permissions)) {
            return true;
        }
        
        return permissions.roles.some(r =>
            r.kind === 'scoped' && 
            (r.role === LanguageScopedRole.Architect || r.role === LanguageScopedRole.DeputyArchitect)
        );
    }

    /**
     * Get the list of languages the user can approve based on their permissions
     */
    getApprovableLanguages(permissions: EffectivePermissions | null | undefined): string[] {
        if (!permissions || !permissions.roles) {
            return [];
        }

        // Admins can approve all languages - return empty to indicate "all"
        // The caller should handle this case specially
        if (this.isAdmin(permissions)) {
            return [];
        }

        const languages: string[] = [];
        for (const role of permissions.roles) {
            if (role.kind === 'scoped' && 
                (role.role === LanguageScopedRole.Architect || role.role === LanguageScopedRole.DeputyArchitect)) {
                if (role.language && !languages.includes(role.language)) {
                    languages.push(role.language);
                }
            }
        }
        return languages.sort();
    }
}
