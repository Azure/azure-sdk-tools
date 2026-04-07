import { Component, OnInit, OnDestroy, ChangeDetectorRef } from '@angular/core';
import { Subject } from 'rxjs';
import { switchMap, finalize, takeUntil } from 'rxjs/operators';
import { ConfirmationService, MessageService } from 'primeng/api';
import { 
    EffectivePermissions, 
    GlobalRole, 
    GroupPermissions, 
    GroupPermissionsRequest, 
    LanguageScopedRole, 
    RoleAssignment,
    SUPPORTED_LANGUAGES,
    GLOBAL_ROLE_OPTIONS,
    LANGUAGE_SCOPED_ROLE_OPTIONS,
    formatRoleName as formatRoleNameFn
} from 'src/app/_models/permissions';
import { PermissionsService } from 'src/app/_services/permissions/permissions.service';
import { UserProfileService } from 'src/app/_services/user-profile/user-profile.service';
import { UserProfile } from 'src/app/_models/userProfile';
import { AutoCompleteCompleteEvent } from 'primeng/autocomplete';

interface GroupFormData {
    groupId: string;
    groupName: string;
    roles: RoleAssignment[];
    serviceNames: string[];
}

@Component({
    selector: 'app-admin-permissions-page',
    templateUrl: './admin-permissions-page.component.html',
    styleUrls: ['./admin-permissions-page.component.scss'],
    standalone: false
})
export class AdminPermissionsPageComponent implements OnInit, OnDestroy {
    currentUserPermissions: EffectivePermissions | null = null;
    isAdmin: boolean = false;
    isLoading: boolean = true;

    private destroy$ = new Subject<void>();

    groups: GroupPermissions[] = [];
    selectedGroup: GroupPermissions | null = null;

    showGroupDialog: boolean = false;
    showAddMemberDialog: boolean = false;
    isEditMode: boolean = false;

    groupForm: GroupFormData = this.getEmptyGroupForm();
    newMemberUsernames: string[] = [];
    userSuggestions: string[] = [];
    allUsers: string[] = []; 

    globalRoleOptions = GLOBAL_ROLE_OPTIONS;
    languageScopedRoleOptions = LANGUAGE_SCOPED_ROLE_OPTIONS;
    languageOptions = SUPPORTED_LANGUAGES;

    newRoleKind: 'global' | 'scoped' = 'global';
    newGlobalRole: GlobalRole = GlobalRole.ServiceTeam;
    newScopedRole: LanguageScopedRole = LanguageScopedRole.Architect;
    newRoleLanguage: string = 'Python';

    private readonly defaultGroupId = 'azure-sdk-team';

    constructor(
        private permissionsService: PermissionsService,
        private userProfileService: UserProfileService,
        private messageService: MessageService,
        private confirmationService: ConfirmationService,
        private cdr: ChangeDetectorRef
    ) {}

    ngOnInit(): void {
        this.loadCurrentUserPermissions();
    }

    ngOnDestroy(): void {
        this.destroy$.next();
        this.destroy$.complete();
    }

    private loadCurrentUserPermissions(): void {
        this.userProfileService.getUserProfile().pipe(
            takeUntil(this.destroy$),
            finalize(() => {
                if (this.isLoading && !this.isAdmin) {
                    this.isLoading = false;
                    this.cdr.detectChanges();
                }
            })
        ).subscribe({
            next: (userProfile: UserProfile) => {
                this.currentUserPermissions = userProfile.permissions;
                this.isAdmin = this.permissionsService.isAdmin(this.currentUserPermissions);

                if (this.isAdmin) {
                    this.loadGroups();
                } else {
                    this.isLoading = false;
                    this.cdr.detectChanges();
                }
            },
            error: () => {
                this.isLoading = false;
                this.cdr.detectChanges();
                this.messageService.add({
                    severity: 'error',
                    summary: 'Error',
                    detail: 'Failed to load user permissions'
                });
            }
        });
    }

    private loadGroups(): void {
        this.isLoading = true;
        this.permissionsService.getAllGroups().pipe(
            takeUntil(this.destroy$),
            switchMap(groups => {
                this.groups = groups;
                if (!this.selectedGroup) {
                    const defaultGroup = groups.find(g => g.groupId === this.defaultGroupId);
                    if (defaultGroup) {
                        this.selectedGroup = defaultGroup;
                    }
                }
                return this.permissionsService.getAllUsernames();
            }),
            finalize(() => {
                this.isLoading = false;
                this.cdr.detectChanges();
            })
        ).subscribe({
            next: (users) => {
                this.allUsers = users;
            },
            error: () => {
                this.allUsers = [];
                this.messageService.add({
                    severity: 'error',
                    summary: 'Error',
                    detail: 'Failed to load groups'
                });
            }
        });
    }

    private getEmptyGroupForm(): GroupFormData {
        return {
            groupId: '',
            groupName: '',
            roles: [],
            serviceNames: []
        };
    }

    openCreateGroupDialog(): void {
        this.isEditMode = false;
        this.groupForm = this.getEmptyGroupForm();
        this.showGroupDialog = true;
    }

    openEditGroupDialog(group: GroupPermissions): void {
        this.isEditMode = true;
        this.groupForm = {
            groupId: group.groupId,
            groupName: group.groupName,
            roles: [...group.roles],
            serviceNames: [...group.serviceNames]
        };
        this.showGroupDialog = true;
    }

    saveGroup(): void {
        const request: GroupPermissionsRequest = {
            groupId: this.groupForm.groupId,
            groupName: this.groupForm.groupName,
            roles: this.groupForm.roles,
            serviceNames: this.groupForm.serviceNames
        };

        if (this.isEditMode) {
            this.permissionsService.updateGroup(this.groupForm.groupId, request).subscribe({
                next: () => {
                    this.messageService.add({
                        severity: 'success',
                        summary: 'Success',
                        detail: 'Group updated successfully'
                    });
                    this.showGroupDialog = false;
                    this.loadGroups();
                },
                error: (err) => {
                    this.messageService.add({
                        severity: 'error',
                        summary: 'Error',
                        detail: 'Failed to update group'
                    });
                }
            });
        } else {
            this.permissionsService.createGroup(request).subscribe({
                next: () => {
                    this.messageService.add({
                        severity: 'success',
                        summary: 'Success',
                        detail: 'Group created successfully'
                    });
                    this.showGroupDialog = false;
                    this.loadGroups();
                },
                error: (err) => {
                    this.messageService.add({
                        severity: 'error',
                        summary: 'Error',
                        detail: 'Failed to create group'
                    });
                }
            });
        }
    }

    deleteGroup(group: GroupPermissions): void {
        this.confirmationService.confirm({
            message: `Are you sure you want to delete the group "${group.groupName}"?`,
            header: 'Delete Group',
            icon: 'pi pi-exclamation-triangle',
            acceptButtonStyleClass: 'p-button-danger',
            accept: () => {
                this.permissionsService.deleteGroup(group.groupId).subscribe({
                    next: () => {
                        this.messageService.add({
                            severity: 'success',
                            summary: 'Success',
                            detail: 'Group deleted successfully'
                        });
                        this.loadGroups();
                    },
                    error: (err) => {
                        this.messageService.add({
                            severity: 'error',
                            summary: 'Error',
                            detail: 'Failed to delete group'
                        });
                    }
                });
            }
        });
    }

    addRoleToForm(): void {
        let newRole: RoleAssignment;
        
        if (this.newRoleKind === 'global') {
            newRole = {
                kind: 'global',
                role: this.newGlobalRole
            };
        } else {
            newRole = {
                kind: 'scoped',
                role: this.newScopedRole,
                language: this.newRoleLanguage
            };
        }

        // Check for duplicates
        const isDuplicate = this.groupForm.roles.some(r => {
            if (r.kind !== newRole.kind) return false;
            if (r.kind === 'global' && newRole.kind === 'global') {
                return r.role === newRole.role;
            }
            if (r.kind === 'scoped' && newRole.kind === 'scoped') {
                return r.role === newRole.role && r.language === newRole.language;
            }
            return false;
        });

        if (isDuplicate) {
            this.messageService.add({
                severity: 'warn',
                summary: 'Warning',
                detail: 'This role already exists in the group'
            });
            return;
        }

        this.groupForm.roles.push(newRole);
    }

    removeRoleFromForm(index: number): void {
        this.groupForm.roles.splice(index, 1);
    }

    getRoleDisplayName(role: RoleAssignment): string {
        const roleName = formatRoleNameFn(role.role);
        if (role.kind === 'global') {
            return `${roleName} (Global)`;
        } else {
            return `${roleName} - ${role.language}`;
        }
    }

    // Wrapper for template access to shared formatRoleName function
    formatRoleName(role: string): string {
        return formatRoleNameFn(role);
    }

    selectGroup(group: GroupPermissions): void {
        this.selectedGroup = group;
    }

    openAddMemberDialog(): void {
        this.newMemberUsernames = [];
        this.userSuggestions = [];
        this.showAddMemberDialog = true;
    }

    searchUsers(event: AutoCompleteCompleteEvent): void {
        const query = (event.query || '').toLowerCase();
        
        const existingMembers = new Set(this.selectedGroup?.members || []);
        const alreadySelected = new Set(this.newMemberUsernames);
        
        this.userSuggestions = this.allUsers.filter(u => {
            if (existingMembers.has(u) || alreadySelected.has(u)) {
                return false;
            }
            return u.toLowerCase().startsWith(query);
        }).slice(0, 20); 
    }

    addMembers(): void {
        if (!this.selectedGroup || this.newMemberUsernames.length === 0) return;

        const groupId = this.selectedGroup.groupId;
        const usernames = this.newMemberUsernames.map(u => u.trim()).filter(u => u.length > 0);
        
        if (usernames.length === 0) return;

        this.permissionsService.addMembersToGroup(groupId, usernames).subscribe({
            next: (result) => {
                if (result.addedUsers.length > 0) {
                    const memberText = result.addedUsers.length === 1 
                        ? `Member "${result.addedUsers[0]}"` 
                        : `${result.addedUsers.length} members`;
                    this.messageService.add({
                        severity: 'success',
                        summary: 'Success',
                        detail: `${memberText} added successfully`
                    });
                }

                if (result.invalidUsers.length > 0) {
                    const invalidText = result.invalidUsers.join(', ');
                    this.messageService.add({
                        severity: 'warn',
                        summary: 'Users Not Found',
                        detail: `The following users don't exist in our database and were not added: ${invalidText}`,
                        sticky: true
                    });
                }

                this.showAddMemberDialog = false;
                this.refreshGroupsAndReselect(groupId);
            },
            error: (err) => {
                this.messageService.add({
                    severity: 'error',
                    summary: 'Error',
                    detail: err.error?.message || 'Failed to add members'
                });
            }
        });
    }

    removeMember(userId: string): void {
        if (!this.selectedGroup) return;

        const groupId = this.selectedGroup.groupId;
        this.confirmationService.confirm({
            message: `Are you sure you want to remove "${userId}" from this group?`,
            header: 'Remove Member',
            icon: 'pi pi-exclamation-triangle',
            acceptButtonStyleClass: 'p-button-danger',
            accept: () => {
                this.permissionsService.removeMemberFromGroup(groupId, userId).subscribe({
                    next: () => {
                        this.messageService.add({
                            severity: 'success',
                            summary: 'Success',
                            detail: `Member "${userId}" removed successfully`
                        });
                        this.refreshGroupsAndReselect(groupId);
                    },
                    error: (err) => {
                        this.messageService.add({
                            severity: 'error',
                            summary: 'Error',
                            detail: 'Failed to remove member'
                        });
                    }
                });
            }
        });
    }

    private refreshGroupsAndReselect(groupId: string): void {
        this.permissionsService.getAllGroups().pipe(
            takeUntil(this.destroy$)
        ).subscribe({
            next: (groups) => {
                this.groups = groups;
                this.selectedGroup = groups.find(g => g.groupId === groupId) || null;
            },
            error: () => {
                this.messageService.add({
                    severity: 'error',
                    summary: 'Error',
                    detail: 'Failed to refresh groups'
                });
            }
        });
    }

    // ===== Helper Methods =====
    private avatarColors = [
        '#6366f1', '#8b5cf6', '#ec4899', '#f43f5e', '#f97316',
        '#eab308', '#22c55e', '#14b8a6', '#06b6d4', '#3b82f6'
    ];

    getGroupColor(index: number): string {
        return this.avatarColors[index % this.avatarColors.length];
    }

    getTotalMembers(): number {
        return this.groups.reduce((total, group) => total + group.members.length, 0);
    }

    formatDate(dateString: string): string {
        return new Date(dateString).toLocaleDateString('en-US', {
            year: 'numeric',
            month: 'short',
            day: 'numeric',
            hour: '2-digit',
            minute: '2-digit'
        });
    }
}
