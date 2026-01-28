import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { PermissionsService } from './permissions.service';
import { ConfigService } from '../config/config.service';
import { EffectivePermissions, GlobalRole, LanguageScopedRole } from 'src/app/_models/permissions';

describe('PermissionsService', () => {
    let service: PermissionsService;
    let configServiceSpy: jasmine.SpyObj<ConfigService>;

    beforeEach(() => {
        const spy = jasmine.createSpyObj('ConfigService', [], { apiUrl: 'http://localhost/' });

        TestBed.configureTestingModule({
            imports: [HttpClientTestingModule],
            providers: [
                PermissionsService,
                { provide: ConfigService, useValue: spy }
            ]
        });

        service = TestBed.inject(PermissionsService);
        configServiceSpy = TestBed.inject(ConfigService) as jasmine.SpyObj<ConfigService>;
    });

    it('should be created', () => {
        expect(service).toBeTruthy();
    });

    describe('hasGlobalRole', () => {
        it('should return false for null permissions', () => {
            expect(service.hasGlobalRole(null, GlobalRole.Admin)).toBeFalse();
        });

        it('should return false for undefined permissions', () => {
            expect(service.hasGlobalRole(undefined, GlobalRole.Admin)).toBeFalse();
        });

        it('should return true when user has the specified global role', () => {
            const permissions: EffectivePermissions = {
                userId: 'testuser',
                roles: [{ kind: 'global', role: GlobalRole.Admin }]
            };
            expect(service.hasGlobalRole(permissions, GlobalRole.Admin)).toBeTrue();
        });

        it('should return false when user does not have the specified global role', () => {
            const permissions: EffectivePermissions = {
                userId: 'testuser',
                roles: [{ kind: 'global', role: GlobalRole.SdkTeam }]
            };
            expect(service.hasGlobalRole(permissions, GlobalRole.Admin)).toBeFalse();
        });

        it('should return true when user has any of the specified global roles', () => {
            const permissions: EffectivePermissions = {
                userId: 'testuser',
                roles: [{ kind: 'global', role: GlobalRole.SdkTeam }]
            };
            expect(service.hasGlobalRole(permissions, [GlobalRole.Admin, GlobalRole.SdkTeam])).toBeTrue();
        });
    });

    describe('hasLanguageRole', () => {
        it('should return false for null permissions', () => {
            expect(service.hasLanguageRole(null, LanguageScopedRole.Architect, 'Python')).toBeFalse();
        });

        it('should return true when user has the specified language role for the correct language', () => {
            const permissions: EffectivePermissions = {
                userId: 'testuser',
                roles: [{ kind: 'scoped', role: LanguageScopedRole.Architect, language: 'Python' }]
            };
            expect(service.hasLanguageRole(permissions, LanguageScopedRole.Architect, 'Python')).toBeTrue();
        });

        it('should return false when user has the role for a different language', () => {
            const permissions: EffectivePermissions = {
                userId: 'testuser',
                roles: [{ kind: 'scoped', role: LanguageScopedRole.Architect, language: 'Python' }]
            };
            expect(service.hasLanguageRole(permissions, LanguageScopedRole.Architect, 'Java')).toBeFalse();
        });

        it('should be case-insensitive for language matching', () => {
            const permissions: EffectivePermissions = {
                userId: 'testuser',
                roles: [{ kind: 'scoped', role: LanguageScopedRole.Architect, language: 'Python' }]
            };
            expect(service.hasLanguageRole(permissions, LanguageScopedRole.Architect, 'python')).toBeTrue();
        });
    });

    describe('canApprove', () => {
        it('should return true for Admin regardless of language', () => {
            const permissions: EffectivePermissions = {
                userId: 'testuser',
                roles: [{ kind: 'global', role: GlobalRole.Admin }]
            };
            expect(service.canApprove(permissions, 'AnyLanguage')).toBeTrue();
        });

        it('should return true for Architect with matching language', () => {
            const permissions: EffectivePermissions = {
                userId: 'testuser',
                roles: [{ kind: 'scoped', role: LanguageScopedRole.Architect, language: 'Python' }]
            };
            expect(service.canApprove(permissions, 'Python')).toBeTrue();
        });

        it('should return true for DeputyArchitect with matching language', () => {
            const permissions: EffectivePermissions = {
                userId: 'testuser',
                roles: [{ kind: 'scoped', role: LanguageScopedRole.DeputyArchitect, language: 'Java' }]
            };
            expect(service.canApprove(permissions, 'Java')).toBeTrue();
        });

        it('should return false for Architect with non-matching language', () => {
            const permissions: EffectivePermissions = {
                userId: 'testuser',
                roles: [{ kind: 'scoped', role: LanguageScopedRole.Architect, language: 'Python' }]
            };
            expect(service.canApprove(permissions, 'Java')).toBeFalse();
        });

        it('should return false for SdkTeam without architect role', () => {
            const permissions: EffectivePermissions = {
                userId: 'testuser',
                roles: [{ kind: 'global', role: GlobalRole.SdkTeam }]
            };
            expect(service.canApprove(permissions, 'Python')).toBeFalse();
        });
    });

    describe('isAdmin', () => {
        it('should return true for Admin', () => {
            const permissions: EffectivePermissions = {
                userId: 'testuser',
                roles: [{ kind: 'global', role: GlobalRole.Admin }]
            };
            expect(service.isAdmin(permissions)).toBeTrue();
        });

        it('should return false for non-Admin', () => {
            const permissions: EffectivePermissions = {
                userId: 'testuser',
                roles: [{ kind: 'global', role: GlobalRole.SdkTeam }]
            };
            expect(service.isAdmin(permissions)).toBeFalse();
        });
    });

    describe('hasElevatedAccess', () => {
        it('should return true for Admin', () => {
            const permissions: EffectivePermissions = {
                userId: 'testuser',
                roles: [{ kind: 'global', role: GlobalRole.Admin }]
            };
            expect(service.hasElevatedAccess(permissions)).toBeTrue();
        });

        it('should return true for SdkTeam', () => {
            const permissions: EffectivePermissions = {
                userId: 'testuser',
                roles: [{ kind: 'global', role: GlobalRole.SdkTeam }]
            };
            expect(service.hasElevatedAccess(permissions)).toBeTrue();
        });

        it('should return false for ServiceTeam', () => {
            const permissions: EffectivePermissions = {
                userId: 'testuser',
                roles: [{ kind: 'global', role: GlobalRole.ServiceTeam }]
            };
            expect(service.hasElevatedAccess(permissions)).toBeFalse();
        });
    });

    describe('getApprovalLanguages', () => {
        it('should return ["*"] for Admin', () => {
            const permissions: EffectivePermissions = {
                userId: 'testuser',
                roles: [{ kind: 'global', role: GlobalRole.Admin }]
            };
            expect(service.getApprovalLanguages(permissions)).toEqual(['*']);
        });

        it('should return list of languages for Architect roles', () => {
            const permissions: EffectivePermissions = {
                userId: 'testuser',
                roles: [
                    { kind: 'scoped', role: LanguageScopedRole.Architect, language: 'Python' },
                    { kind: 'scoped', role: LanguageScopedRole.DeputyArchitect, language: 'Java' }
                ]
            };
            const languages = service.getApprovalLanguages(permissions);
            expect(languages).toContain('Python');
            expect(languages).toContain('Java');
            expect(languages.length).toBe(2);
        });

        it('should return empty array for no approval roles', () => {
            const permissions: EffectivePermissions = {
                userId: 'testuser',
                roles: [{ kind: 'global', role: GlobalRole.SdkTeam }]
            };
            expect(service.getApprovalLanguages(permissions)).toEqual([]);
        });
    });
});
