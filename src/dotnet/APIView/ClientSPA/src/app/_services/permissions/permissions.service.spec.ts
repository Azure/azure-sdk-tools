import { TestBed } from '@angular/core/testing';
import { initializeTestBed } from '../../../test-setup';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { PermissionsService } from './permissions.service';
import { ConfigService } from '../config/config.service';
import { EffectivePermissions, GlobalRole, LanguageScopedRole } from 'src/app/_models/permissions';

describe('PermissionsService', () => {
    let service: PermissionsService;

    beforeAll(() => {
        initializeTestBed();
    });

    beforeEach(() => {
        const mockConfigService = {
            apiUrl: 'http://localhost/'
        };

        TestBed.configureTestingModule({
            providers: [
                provideHttpClient(),
                provideHttpClientTesting(),
                PermissionsService,
                { provide: ConfigService, useValue: mockConfigService }
            ]
        });

        service = TestBed.inject(PermissionsService);
    });

    it('should be created', () => {
        expect(service).toBeTruthy();
    });

    describe('hasGlobalRole', () => {
        it('should return false for null permissions', () => {
            expect(service.hasGlobalRole(null, GlobalRole.Admin)).toBe(false);
        });

        it('should return false for undefined permissions', () => {
            expect(service.hasGlobalRole(undefined, GlobalRole.Admin)).toBe(false);
        });

        it('should return true when user has the specified global role', () => {
            const permissions: EffectivePermissions = {
                userId: 'testuser',
                roles: [{ kind: 'global', role: GlobalRole.Admin }]
            };
            expect(service.hasGlobalRole(permissions, GlobalRole.Admin)).toBe(true);
        });

        it('should return false when user does not have the specified global role', () => {
            const permissions: EffectivePermissions = {
                userId: 'testuser',
                roles: [{ kind: 'global', role: GlobalRole.SdkTeam }]
            };
            expect(service.hasGlobalRole(permissions, GlobalRole.Admin)).toBe(false);
        });

        it('should return true when user has any of the specified global roles', () => {
            const permissions: EffectivePermissions = {
                userId: 'testuser',
                roles: [{ kind: 'global', role: GlobalRole.SdkTeam }]
            };
            expect(service.hasGlobalRole(permissions, [GlobalRole.Admin, GlobalRole.SdkTeam])).toBe(true);
        });
    });

    describe('hasLanguageRole', () => {
        it('should return false for null permissions', () => {
            expect(service.hasLanguageRole(null, LanguageScopedRole.Architect, 'Python')).toBe(false);
        });

        it('should return true when user has the specified language role for the correct language', () => {
            const permissions: EffectivePermissions = {
                userId: 'testuser',
                roles: [{ kind: 'scoped', role: LanguageScopedRole.Architect, language: 'Python' }]
            };
            expect(service.hasLanguageRole(permissions, LanguageScopedRole.Architect, 'Python')).toBe(true);
        });

        it('should return false when user has the role for a different language', () => {
            const permissions: EffectivePermissions = {
                userId: 'testuser',
                roles: [{ kind: 'scoped', role: LanguageScopedRole.Architect, language: 'Python' }]
            };
            expect(service.hasLanguageRole(permissions, LanguageScopedRole.Architect, 'Java')).toBe(false);
        });

        it('should be case-insensitive for language matching', () => {
            const permissions: EffectivePermissions = {
                userId: 'testuser',
                roles: [{ kind: 'scoped', role: LanguageScopedRole.Architect, language: 'Python' }]
            };
            expect(service.hasLanguageRole(permissions, LanguageScopedRole.Architect, 'python')).toBe(true);
        });
    });

    describe('isApproverFor', () => {
        it('should return true for Admin regardless of language', () => {
            const permissions: EffectivePermissions = {
                userId: 'testuser',
                roles: [{ kind: 'global', role: GlobalRole.Admin }]
            };
            expect(service.isApproverFor(permissions, 'AnyLanguage')).toBe(true);
        });

        it('should return true for Architect with matching language', () => {
            const permissions: EffectivePermissions = {
                userId: 'testuser',
                roles: [{ kind: 'scoped', role: LanguageScopedRole.Architect, language: 'Python' }]
            };
            expect(service.isApproverFor(permissions, 'Python')).toBe(true);
        });

        it('should return true for DeputyArchitect with matching language', () => {
            const permissions: EffectivePermissions = {
                userId: 'testuser',
                roles: [{ kind: 'scoped', role: LanguageScopedRole.DeputyArchitect, language: 'Java' }]
            };
            expect(service.isApproverFor(permissions, 'Java')).toBe(true);
        });

        it('should return false for Architect with non-matching language', () => {
            const permissions: EffectivePermissions = {
                userId: 'testuser',
                roles: [{ kind: 'scoped', role: LanguageScopedRole.Architect, language: 'Python' }]
            };
            expect(service.isApproverFor(permissions, 'Java')).toBe(false);
        });

        it('should return false for SdkTeam without architect role', () => {
            const permissions: EffectivePermissions = {
                userId: 'testuser',
                roles: [{ kind: 'global', role: GlobalRole.SdkTeam }]
            };
            expect(service.isApproverFor(permissions, 'Python')).toBe(false);
        });
    });

    describe('isAdmin', () => {
        it('should return true for Admin', () => {
            const permissions: EffectivePermissions = {
                userId: 'testuser',
                roles: [{ kind: 'global', role: GlobalRole.Admin }]
            };
            expect(service.isAdmin(permissions)).toBe(true);
        });

        it('should return false for non-Admin', () => {
            const permissions: EffectivePermissions = {
                userId: 'testuser',
                roles: [{ kind: 'global', role: GlobalRole.SdkTeam }]
            };
            expect(service.isAdmin(permissions)).toBe(false);
        });
    });
});
