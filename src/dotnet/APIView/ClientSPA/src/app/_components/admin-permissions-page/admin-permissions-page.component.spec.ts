import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { initializeTestBed } from '../../../test-setup';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { FormsModule } from '@angular/forms';
import { of, throwError } from 'rxjs';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { vi, Mock } from 'vitest';

import { AdminPermissionsPageComponent } from './admin-permissions-page.component';
import { PermissionsService } from 'src/app/_services/permissions/permissions.service';
import { UserProfileService } from 'src/app/_services/user-profile/user-profile.service';
import { ConfirmationService, MessageService } from 'primeng/api';
import { SelectModule } from 'primeng/select';
import { SelectButtonModule } from 'primeng/selectbutton';
import { AutoCompleteModule } from 'primeng/autocomplete';
import { 
    AddMembersResult,
    EffectivePermissions, 
    GlobalRole, 
    GroupPermissions, 
    LanguageScopedRole 
} from 'src/app/_models/permissions';
import { UserProfile } from 'src/app/_models/userProfile';

interface MockPermissionsService {
    isAdmin: Mock;
    getAllGroups: Mock;
    createGroup: Mock;
    updateGroup: Mock;
    deleteGroup: Mock;
    addMembersToGroup: Mock;
    removeMemberFromGroup: Mock;
    getAllUsernames: Mock;
}

interface MockUserProfileService {
    getUserProfile: Mock;
}

interface MockMessageService {
    add: Mock;
}

interface MockConfirmationService {
    confirm: Mock;
}

describe('AdminPermissionsPageComponent', () => {
    let component: AdminPermissionsPageComponent;
    let fixture: ComponentFixture<AdminPermissionsPageComponent>;
    let permissionsServiceSpy: MockPermissionsService;
    let userProfileServiceSpy: MockUserProfileService;
    let messageServiceSpy: MockMessageService;
    let confirmationServiceSpy: MockConfirmationService;

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
            language: []
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

    beforeAll(() => {
        initializeTestBed();
    });

    beforeEach(async () => {
        const permissionsSpy: MockPermissionsService = {
            isAdmin: vi.fn(),
            getAllGroups: vi.fn(),
            createGroup: vi.fn(),
            updateGroup: vi.fn(),
            deleteGroup: vi.fn(),
            addMembersToGroup: vi.fn(),
            removeMemberFromGroup: vi.fn(),
            getAllUsernames: vi.fn()
        };
        const userProfileSpy: MockUserProfileService = {
            getUserProfile: vi.fn()
        };
        const messageSpy: MockMessageService = {
            add: vi.fn()
        };
        const confirmationSpy: MockConfirmationService = {
            confirm: vi.fn()
        };

        await TestBed.configureTestingModule({
            declarations: [AdminPermissionsPageComponent],
            imports: [
                FormsModule,
                BrowserAnimationsModule,
                SelectModule,
                SelectButtonModule,
                AutoCompleteModule
            ],
            providers: [
                provideHttpClient(),
                provideHttpClientTesting(),
                { provide: PermissionsService, useValue: permissionsSpy },
                { provide: UserProfileService, useValue: userProfileSpy },
                { provide: MessageService, useValue: messageSpy },
                { provide: ConfirmationService, useValue: confirmationSpy }
            ],
            schemas: [NO_ERRORS_SCHEMA]
        }).compileComponents();

        fixture = TestBed.createComponent(AdminPermissionsPageComponent);
        component = fixture.componentInstance;
        permissionsServiceSpy = TestBed.inject(PermissionsService) as unknown as MockPermissionsService;
        userProfileServiceSpy = TestBed.inject(UserProfileService) as unknown as MockUserProfileService;
        messageServiceSpy = TestBed.inject(MessageService) as unknown as MockMessageService;
        confirmationServiceSpy = TestBed.inject(ConfirmationService) as unknown as MockConfirmationService;

        // Default mock for getAllUsernames - called during component initialization
        permissionsServiceSpy.getAllUsernames.mockReturnValue(of(['user1', 'user2', 'pythonDev1', 'newUser']));
    });

    describe('Initialization', () => {
        it('should create', () => {
            userProfileServiceSpy.getUserProfile.mockReturnValue(of(mockUserProfile));
            permissionsServiceSpy.isAdmin.mockReturnValue(true);
            permissionsServiceSpy.getAllGroups.mockReturnValue(of(mockGroups));
            
            fixture.detectChanges();
            
            expect(component).toBeTruthy();
        });

        it('should load groups when user is admin', fakeAsync(() => {
            userProfileServiceSpy.getUserProfile.mockReturnValue(of(mockUserProfile));
            permissionsServiceSpy.isAdmin.mockReturnValue(true);
            permissionsServiceSpy.getAllGroups.mockReturnValue(of(mockGroups));

            fixture.detectChanges();
            tick();

            expect(component.isAdmin).toBe(true);
            expect(component.groups.length).toBe(2);
            expect(component.isLoading).toBe(false);
        }));

        it('should not load groups when user is not admin', fakeAsync(() => {
            const nonAdminProfile = { ...mockUserProfile, permissions: mockNonAdminPermissions };
            userProfileServiceSpy.getUserProfile.mockReturnValue(of(nonAdminProfile));
            permissionsServiceSpy.isAdmin.mockReturnValue(false);

            fixture.detectChanges();
            tick();

            expect(component.isAdmin).toBe(false);
            expect(component.groups.length).toBe(0);
            expect(permissionsServiceSpy.getAllGroups).not.toHaveBeenCalled();
        }));

        it('should handle error when loading user profile', fakeAsync(() => {
            userProfileServiceSpy.getUserProfile.mockReturnValue(throwError(() => new Error('Failed')));

            fixture.detectChanges();
            tick();

            expect(component.isLoading).toBe(false);
            expect(messageServiceSpy.add).toHaveBeenCalledWith(expect.objectContaining({
                severity: 'error',
                summary: 'Error'
            }));
        }));
    });

    describe('Group Selection', () => {
        beforeEach(() => {
            userProfileServiceSpy.getUserProfile.mockReturnValue(of(mockUserProfile));
            permissionsServiceSpy.isAdmin.mockReturnValue(true);
            permissionsServiceSpy.getAllGroups.mockReturnValue(of(mockGroups));
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
            userProfileServiceSpy.getUserProfile.mockReturnValue(of(mockUserProfile));
            permissionsServiceSpy.isAdmin.mockReturnValue(true);
            permissionsServiceSpy.getAllGroups.mockReturnValue(of(mockGroups));
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
            expect(messageServiceSpy.add).toHaveBeenCalledWith(expect.objectContaining({
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
            userProfileServiceSpy.getUserProfile.mockReturnValue(of(mockUserProfile));
            permissionsServiceSpy.isAdmin.mockReturnValue(true);
            permissionsServiceSpy.getAllGroups.mockReturnValue(of(mockGroups));
            fixture.detectChanges();
        });

        it('should open create group dialog with empty form', () => {
            component.openCreateGroupDialog();
            
            expect(component.showGroupDialog).toBe(true);
            expect(component.isEditMode).toBe(false);
            expect(component.groupForm.groupId).toBe('');
            expect(component.groupForm.groupName).toBe('');
        });

        it('should open edit group dialog with group data', () => {
            component.openEditGroupDialog(mockGroups[0]);
            
            expect(component.showGroupDialog).toBe(true);
            expect(component.isEditMode).toBe(true);
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
            permissionsServiceSpy.createGroup.mockReturnValue(of(newGroup));
            
            component.openCreateGroupDialog();
            component.groupForm.groupId = 'new-group';
            component.groupForm.groupName = 'New Group';
            
            component.saveGroup();
            tick();
            
            expect(permissionsServiceSpy.createGroup).toHaveBeenCalled();
            expect(messageServiceSpy.add).toHaveBeenCalledWith(expect.objectContaining({
                severity: 'success'
            }));
        }));

        it('should update group successfully', fakeAsync(() => {
            permissionsServiceSpy.updateGroup.mockReturnValue(of(mockGroups[0]));
            
            component.openEditGroupDialog(mockGroups[0]);
            component.groupForm.groupName = 'Updated Name';
            
            component.saveGroup();
            tick();
            
            expect(permissionsServiceSpy.updateGroup).toHaveBeenCalledWith(
                'admins',
                expect.objectContaining({ groupName: 'Updated Name' })
            );
        }));

        it('should delete group after confirmation', fakeAsync(() => {
            confirmationServiceSpy.confirm.mockImplementation((config: any) => {
                config.accept();
                return confirmationServiceSpy;
            });
            permissionsServiceSpy.deleteGroup.mockReturnValue(of(void 0));
            
            component.deleteGroup(mockGroups[0]);
            tick();
            
            expect(permissionsServiceSpy.deleteGroup).toHaveBeenCalledWith('admins');
        }));

        it('should not delete group when confirmation is cancelled', () => {
            confirmationServiceSpy.confirm.mockImplementation((config: any) => {
                // Do nothing - simulates user clicking cancel
                return confirmationServiceSpy;
            });
            
            component.deleteGroup(mockGroups[0]);
            
            expect(permissionsServiceSpy.deleteGroup).not.toHaveBeenCalled();
        });
    });

    describe('Member Management', () => {
        beforeEach(() => {
            userProfileServiceSpy.getUserProfile.mockReturnValue(of(mockUserProfile));
            permissionsServiceSpy.isAdmin.mockReturnValue(true);
            permissionsServiceSpy.getAllGroups.mockReturnValue(of(mockGroups));
            fixture.detectChanges();
            component.selectGroup(mockGroups[0]);
        });

        it('should open add member dialog', () => {
            component.openAddMemberDialog();
            
            expect(component.showAddMemberDialog).toBe(true);
            expect(component.newMemberUsernames).toEqual([]);
        });

        it('should add member successfully', fakeAsync(() => {
            const mockResult: AddMembersResult = { addedUsers: ['newUser'], invalidUsers: [], allUsersValid: true };
            permissionsServiceSpy.addMembersToGroup.mockReturnValue(of(mockResult));
            permissionsServiceSpy.getAllGroups.mockReturnValue(of(mockGroups));
            
            component.openAddMemberDialog();
            component.newMemberUsernames = ['newUser'];
            component.addMembers();
            tick();
            
            expect(permissionsServiceSpy.addMembersToGroup).toHaveBeenCalledWith('admins', ['newUser']);
            expect(messageServiceSpy.add).toHaveBeenCalledWith(expect.objectContaining({
                severity: 'success'
            }));
        }));

        it('should not add member with empty usernames', () => {
            component.openAddMemberDialog();
            component.newMemberUsernames = [];
            component.addMembers();
            
            expect(permissionsServiceSpy.addMembersToGroup).not.toHaveBeenCalled();
        });

        it('should remove member after confirmation', fakeAsync(() => {
            confirmationServiceSpy.confirm.mockImplementation((config: any) => {
                config.accept();
                return confirmationServiceSpy;
            });
            permissionsServiceSpy.removeMemberFromGroup.mockReturnValue(of(void 0));
            permissionsServiceSpy.getAllGroups.mockReturnValue(of(mockGroups));
            
            component.removeMember('user1');
            tick();
            
            expect(permissionsServiceSpy.removeMemberFromGroup).toHaveBeenCalledWith('admins', 'user1');
        }));
    });

    describe('Helper Methods', () => {
        beforeEach(() => {
            userProfileServiceSpy.getUserProfile.mockReturnValue(of(mockUserProfile));
            permissionsServiceSpy.isAdmin.mockReturnValue(true);
            permissionsServiceSpy.getAllGroups.mockReturnValue(of(mockGroups));
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
