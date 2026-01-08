import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { FormsModule } from '@angular/forms';
import { of, throwError } from 'rxjs';

import { AdminPermissionsPageComponent } from './admin-permissions-page.component';
import { PermissionsService } from 'src/app/_services/permissions/permissions.service';
import { UserProfileService } from 'src/app/_services/user-profile/user-profile.service';
import { ConfirmationService, MessageService } from 'primeng/api';
import { 
    EffectivePermissions, 
    GlobalRole, 
    GroupPermissions, 
    LanguageScopedRole 
} from 'src/app/_models/permissions';
import { UserProfile } from 'src/app/_models/userProfile';

describe('AdminPermissionsPageComponent', () => {
    let component: AdminPermissionsPageComponent;
    let fixture: ComponentFixture<AdminPermissionsPageComponent>;
    let permissionsServiceSpy: jasmine.SpyObj<PermissionsService>;
    let userProfileServiceSpy: jasmine.SpyObj<UserProfileService>;
    let messageServiceSpy: jasmine.SpyObj<MessageService>;
    let confirmationServiceSpy: jasmine.SpyObj<ConfirmationService>;

    const mockAdminPermissions: EffectivePermissions = {
        userId: 'adminUser',
        roles: [{ kind: 'global', role: GlobalRole.Admin }]
    };

    const mockNonAdminPermissions: EffectivePermissions = {
        userId: 'regularUser',
        roles: [{ kind: 'global', role: GlobalRole.ServiceTeam }]
    };

    const mockUserProfile: UserProfile = {
        userName: 'adminUser',
        email: 'admin@test.com',
        languages: [],
        preferences: {
            userName: 'adminUser',
            theme: 'light-theme',
            hideLineNumbers: false,
            hideLeftNavigation: false,
            showHiddenApis: false,
            hideReviewPageOptions: false,
            hideSamplesPageOptions: false,
            showDocumentation: true,
            showComments: true,
            showSystemComments: true,
            disableCodeLinesLazyLoading: false,
            scrollBarSize: 'small' as any,
            language: [],
            approvedLanguages: []
        },
        permissions: mockAdminPermissions
    };

    const mockGroups: GroupPermissions[] = [
        {
            id: 'group-admins',
            type: 'group',
            groupId: 'admins',
            groupName: 'Administrators',
            roles: [{ kind: 'global', role: GlobalRole.Admin }],
            members: ['user1', 'user2'],
            lastUpdatedOn: '2026-01-07T00:00:00Z',
            lastUpdatedBy: 'system',
            serviceNames: []
        },
        {
            id: 'group-python-architects',
            type: 'group',
            groupId: 'python-architects',
            groupName: 'Python Architects',
            roles: [{ kind: 'scoped', role: LanguageScopedRole.Architect, language: 'Python' }],
            members: ['pythonDev1'],
            lastUpdatedOn: '2026-01-07T00:00:00Z',
            lastUpdatedBy: 'system',
            serviceNames: ['azure-sdk-for-python']
        }
    ];

    beforeEach(async () => {
        const permissionsSpy = jasmine.createSpyObj('PermissionsService', [
            'isAdmin', 
            'getAllGroups', 
            'createGroup', 
            'updateGroup', 
            'deleteGroup',
            'addMembersToGroup',
            'removeMemberFromGroup'
        ]);
        const userProfileSpy = jasmine.createSpyObj('UserProfileService', ['getUserProfile']);
        const messageSpy = jasmine.createSpyObj('MessageService', ['add']);
        const confirmationSpy = jasmine.createSpyObj('ConfirmationService', ['confirm']);

        await TestBed.configureTestingModule({
            declarations: [AdminPermissionsPageComponent],
            imports: [HttpClientTestingModule, FormsModule],
            providers: [
                { provide: PermissionsService, useValue: permissionsSpy },
                { provide: UserProfileService, useValue: userProfileSpy },
                { provide: MessageService, useValue: messageSpy },
                { provide: ConfirmationService, useValue: confirmationSpy }
            ]
        }).compileComponents();

        fixture = TestBed.createComponent(AdminPermissionsPageComponent);
        component = fixture.componentInstance;
        permissionsServiceSpy = TestBed.inject(PermissionsService) as jasmine.SpyObj<PermissionsService>;
        userProfileServiceSpy = TestBed.inject(UserProfileService) as jasmine.SpyObj<UserProfileService>;
        messageServiceSpy = TestBed.inject(MessageService) as jasmine.SpyObj<MessageService>;
        confirmationServiceSpy = TestBed.inject(ConfirmationService) as jasmine.SpyObj<ConfirmationService>;
    });

    describe('Initialization', () => {
        it('should create', () => {
            userProfileServiceSpy.getUserProfile.and.returnValue(of(mockUserProfile));
            permissionsServiceSpy.isAdmin.and.returnValue(true);
            permissionsServiceSpy.getAllGroups.and.returnValue(of(mockGroups));
            
            fixture.detectChanges();
            
            expect(component).toBeTruthy();
        });

        it('should load groups when user is admin', fakeAsync(() => {
            userProfileServiceSpy.getUserProfile.and.returnValue(of(mockUserProfile));
            permissionsServiceSpy.isAdmin.and.returnValue(true);
            permissionsServiceSpy.getAllGroups.and.returnValue(of(mockGroups));

            fixture.detectChanges();
            tick();

            expect(component.isAdmin).toBeTrue();
            expect(component.groups.length).toBe(2);
            expect(component.isLoading).toBeFalse();
        }));

        it('should not load groups when user is not admin', fakeAsync(() => {
            const nonAdminProfile = { ...mockUserProfile, permissions: mockNonAdminPermissions };
            userProfileServiceSpy.getUserProfile.and.returnValue(of(nonAdminProfile));
            permissionsServiceSpy.isAdmin.and.returnValue(false);

            fixture.detectChanges();
            tick();

            expect(component.isAdmin).toBeFalse();
            expect(component.groups.length).toBe(0);
            expect(permissionsServiceSpy.getAllGroups).not.toHaveBeenCalled();
        }));

        it('should handle error when loading user profile', fakeAsync(() => {
            userProfileServiceSpy.getUserProfile.and.returnValue(throwError(() => new Error('Failed')));

            fixture.detectChanges();
            tick();

            expect(component.isLoading).toBeFalse();
            expect(messageServiceSpy.add).toHaveBeenCalledWith(jasmine.objectContaining({
                severity: 'error',
                summary: 'Error'
            }));
        }));
    });

    describe('Group Selection', () => {
        beforeEach(() => {
            userProfileServiceSpy.getUserProfile.and.returnValue(of(mockUserProfile));
            permissionsServiceSpy.isAdmin.and.returnValue(true);
            permissionsServiceSpy.getAllGroups.and.returnValue(of(mockGroups));
            fixture.detectChanges();
        });

        it('should select a group when clicked', () => {
            component.selectGroup(mockGroups[0]);
            
            expect(component.selectedGroup).toEqual(mockGroups[0]);
        });

        it('should update selected group when a different group is clicked', () => {
            component.selectGroup(mockGroups[0]);
            component.selectGroup(mockGroups[1]);
            
            expect(component.selectedGroup).toEqual(mockGroups[1]);
        });
    });

    describe('Role Management', () => {
        beforeEach(() => {
            userProfileServiceSpy.getUserProfile.and.returnValue(of(mockUserProfile));
            permissionsServiceSpy.isAdmin.and.returnValue(true);
            permissionsServiceSpy.getAllGroups.and.returnValue(of(mockGroups));
            fixture.detectChanges();
            component.openCreateGroupDialog();
        });

        it('should add global role to form', () => {
            component.newRoleKind = 'global';
            component.newGlobalRole = GlobalRole.Admin;
            
            component.addRoleToForm();
            
            expect(component.groupForm.roles.length).toBe(1);
            expect(component.groupForm.roles[0].kind).toBe('global');
        });

        it('should add scoped role to form', () => {
            component.newRoleKind = 'scoped';
            component.newScopedRole = LanguageScopedRole.Architect;
            component.newRoleLanguage = 'Python';
            
            component.addRoleToForm();
            
            expect(component.groupForm.roles.length).toBe(1);
            expect(component.groupForm.roles[0].kind).toBe('scoped');
        });

        it('should prevent adding duplicate global role', () => {
            component.newRoleKind = 'global';
            component.newGlobalRole = GlobalRole.Admin;
            
            component.addRoleToForm();
            component.addRoleToForm();
            
            expect(component.groupForm.roles.length).toBe(1);
            expect(messageServiceSpy.add).toHaveBeenCalledWith(jasmine.objectContaining({
                severity: 'warn'
            }));
        });

        it('should prevent adding duplicate scoped role', () => {
            component.newRoleKind = 'scoped';
            component.newScopedRole = LanguageScopedRole.Architect;
            component.newRoleLanguage = 'Python';
            
            component.addRoleToForm();
            component.addRoleToForm();
            
            expect(component.groupForm.roles.length).toBe(1);
        });

        it('should remove role from form', () => {
            component.newRoleKind = 'global';
            component.newGlobalRole = GlobalRole.Admin;
            component.addRoleToForm();
            
            component.removeRoleFromForm(0);
            
            expect(component.groupForm.roles.length).toBe(0);
        });
    });

    describe('Group CRUD Operations', () => {
        beforeEach(() => {
            userProfileServiceSpy.getUserProfile.and.returnValue(of(mockUserProfile));
            permissionsServiceSpy.isAdmin.and.returnValue(true);
            permissionsServiceSpy.getAllGroups.and.returnValue(of(mockGroups));
            fixture.detectChanges();
        });

        it('should open create group dialog with empty form', () => {
            component.openCreateGroupDialog();
            
            expect(component.showGroupDialog).toBeTrue();
            expect(component.isEditMode).toBeFalse();
            expect(component.groupForm.groupId).toBe('');
            expect(component.groupForm.groupName).toBe('');
        });

        it('should open edit group dialog with group data', () => {
            component.openEditGroupDialog(mockGroups[0]);
            
            expect(component.showGroupDialog).toBeTrue();
            expect(component.isEditMode).toBeTrue();
            expect(component.groupForm.groupId).toBe('admins');
            expect(component.groupForm.groupName).toBe('Administrators');
        });

        it('should create group successfully', fakeAsync(() => {
            const newGroup: GroupPermissions = {
                id: 'group-new',
                type: 'group',
                groupId: 'new-group',
                groupName: 'New Group',
                roles: [],
                members: [],
                lastUpdatedOn: '2026-01-07T00:00:00Z',
                lastUpdatedBy: 'adminUser',
                serviceNames: []
            };
            permissionsServiceSpy.createGroup.and.returnValue(of(newGroup));
            
            component.openCreateGroupDialog();
            component.groupForm.groupId = 'new-group';
            component.groupForm.groupName = 'New Group';
            
            component.saveGroup();
            tick();
            
            expect(permissionsServiceSpy.createGroup).toHaveBeenCalled();
            expect(messageServiceSpy.add).toHaveBeenCalledWith(jasmine.objectContaining({
                severity: 'success'
            }));
        }));

        it('should update group successfully', fakeAsync(() => {
            permissionsServiceSpy.updateGroup.and.returnValue(of(mockGroups[0]));
            
            component.openEditGroupDialog(mockGroups[0]);
            component.groupForm.groupName = 'Updated Name';
            
            component.saveGroup();
            tick();
            
            expect(permissionsServiceSpy.updateGroup).toHaveBeenCalledWith(
                'admins',
                jasmine.objectContaining({ groupName: 'Updated Name' })
            );
        }));

        it('should delete group after confirmation', fakeAsync(() => {
            confirmationServiceSpy.confirm.and.callFake((config: any) => {
                config.accept();
            });
            permissionsServiceSpy.deleteGroup.and.returnValue(of(void 0));
            
            component.deleteGroup(mockGroups[0]);
            tick();
            
            expect(permissionsServiceSpy.deleteGroup).toHaveBeenCalledWith('admins');
        }));

        it('should not delete group when confirmation is cancelled', () => {
            confirmationServiceSpy.confirm.and.callFake((config: any) => {
                // Do nothing - simulates user clicking cancel
            });
            
            component.deleteGroup(mockGroups[0]);
            
            expect(permissionsServiceSpy.deleteGroup).not.toHaveBeenCalled();
        });
    });

    describe('Member Management', () => {
        beforeEach(() => {
            userProfileServiceSpy.getUserProfile.and.returnValue(of(mockUserProfile));
            permissionsServiceSpy.isAdmin.and.returnValue(true);
            permissionsServiceSpy.getAllGroups.and.returnValue(of(mockGroups));
            fixture.detectChanges();
            component.selectGroup(mockGroups[0]);
        });

        it('should open add member dialog', () => {
            component.openAddMemberDialog();
            
            expect(component.showAddMemberDialog).toBeTrue();
            expect(component.newMemberUsername).toBe('');
        });

        it('should add member successfully', fakeAsync(() => {
            permissionsServiceSpy.addMembersToGroup.and.returnValue(of(void 0));
            permissionsServiceSpy.getAllGroups.and.returnValue(of(mockGroups));
            
            component.openAddMemberDialog();
            component.newMemberUsername = 'newUser';
            component.addMember();
            tick();
            
            expect(permissionsServiceSpy.addMembersToGroup).toHaveBeenCalledWith('admins', ['newUser']);
            expect(messageServiceSpy.add).toHaveBeenCalledWith(jasmine.objectContaining({
                severity: 'success'
            }));
        }));

        it('should not add member with empty username', () => {
            component.openAddMemberDialog();
            component.newMemberUsername = '  ';
            component.addMember();
            
            expect(permissionsServiceSpy.addMembersToGroup).not.toHaveBeenCalled();
        });

        it('should remove member after confirmation', fakeAsync(() => {
            confirmationServiceSpy.confirm.and.callFake((config: any) => {
                config.accept();
            });
            permissionsServiceSpy.removeMemberFromGroup.and.returnValue(of(void 0));
            permissionsServiceSpy.getAllGroups.and.returnValue(of(mockGroups));
            
            component.removeMember('user1');
            tick();
            
            expect(permissionsServiceSpy.removeMemberFromGroup).toHaveBeenCalledWith('admins', 'user1');
        }));
    });

    describe('Helper Methods', () => {
        beforeEach(() => {
            userProfileServiceSpy.getUserProfile.and.returnValue(of(mockUserProfile));
            permissionsServiceSpy.isAdmin.and.returnValue(true);
            permissionsServiceSpy.getAllGroups.and.returnValue(of(mockGroups));
            fixture.detectChanges();
        });

        it('should calculate total members correctly', () => {
            expect(component.getTotalMembers()).toBe(3); // 2 + 1 from mock groups
        });

        it('should format role display name for global role', () => {
            const role = { kind: 'global' as const, role: GlobalRole.Admin };
            const displayName = component.getRoleDisplayName(role);
            
            expect(displayName).toContain('Admin');
            expect(displayName).toContain('Global');
        });

        it('should format role display name for scoped role', () => {
            const role = { kind: 'scoped' as const, role: LanguageScopedRole.Architect, language: 'Python' };
            const displayName = component.getRoleDisplayName(role);
            
            expect(displayName).toContain('Architect');
            expect(displayName).toContain('Python');
        });

        it('should format date correctly', () => {
            const dateString = '2026-01-07T10:30:00Z';
            const formatted = component.formatDate(dateString);
            
            expect(formatted).toContain('Jan');
            expect(formatted).toContain('2026');
        });
    });
});
