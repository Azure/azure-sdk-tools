import { ComponentFixture, TestBed } from '@angular/core/testing';
import { initializeTestBed } from '../../../../test-setup';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { NavBarComponent } from './nav-bar.component';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { of } from 'rxjs';
import { vi, Mock } from 'vitest';
import { PermissionsService } from 'src/app/_services/permissions/permissions.service';
import { AuthService } from 'src/app/_services/auth/auth.service';
import { UserProfileService } from 'src/app/_services/user-profile/user-profile.service';
import { ConfigService } from 'src/app/_services/config/config.service';
import { NotificationsService } from 'src/app/_services/notifications/notifications.service';
import { EffectivePermissions, GlobalRole, GlobalRoleAssignment } from 'src/app/_models/permissions';
import { UserProfile } from 'src/app/_models/userProfile';
import { UserPreferenceModel } from 'src/app/_models/userPreferenceModel';
import { SignalRService } from 'src/app/_services/signal-r/signal-r.service';

import { createMockSignalRService, createMockNotificationsService, createMockWorkerService } from 'src/test-helpers/mock-services';

interface MockPermissionsService {
  getMyPermissions: Mock;
  isAdmin: Mock;
  isLanguageApprover: Mock;
}

interface MockAuthService {
  isLoggedIn: Mock;
}

interface MockUserProfileService {
  getUserProfile: Mock;
  updateUserProfile: Mock;
}

interface MockNotificationsServiceType {
  clearNotification: Mock;
  clearAll: Mock;
  notifications$: ReturnType<typeof of>;
}

interface MockConfigService {
  webAppUrl: string;
  apiUrl: string;
}

describe('NavBarComponent', () => {
  let component: NavBarComponent;
  let fixture: ComponentFixture<NavBarComponent>;
  let mockPermissionsService: MockPermissionsService;
  let mockAuthService: MockAuthService;
  let mockUserProfileService: MockUserProfileService;
  let mockNotificationsService: MockNotificationsServiceType;
  let mockConfigService: MockConfigService;

  const mockUserProfile: UserProfile = {
    userName: 'testuser',
    email: 'test@example.com',
    languages: [],
    preferences: new UserPreferenceModel(),
    permissions: null
  };

  const mockPermissions: EffectivePermissions = {
    userId: 'testuser',
    roles: []
  };

  const mockAdminPermissions: EffectivePermissions = {
    userId: 'testuser',
    roles: [{ kind: 'global', role: GlobalRole.Admin } as GlobalRoleAssignment]
  };

  const mockSignalRService = createMockSignalRService();

  beforeAll(() => {
    initializeTestBed();
  });

  beforeEach(() => {
    mockPermissionsService = {
      getMyPermissions: vi.fn(),
      isAdmin: vi.fn(),
      isLanguageApprover: vi.fn()
    };
    mockAuthService = {
      isLoggedIn: vi.fn()
    };
    mockUserProfileService = {
      getUserProfile: vi.fn(),
      updateUserProfile: vi.fn()
    };
    mockNotificationsService = {
      clearNotification: vi.fn(),
      clearAll: vi.fn(),
      notifications$: of([])
    };
    mockConfigService = {
      webAppUrl: 'http://localhost/',
      apiUrl: '/api'
    };

    mockAuthService.isLoggedIn.mockReturnValue(of(true));
    mockUserProfileService.getUserProfile.mockReturnValue(of(mockUserProfile));
    mockPermissionsService.getMyPermissions.mockReturnValue(of(mockPermissions));
    mockPermissionsService.isAdmin.mockReturnValue(false);
    mockPermissionsService.isLanguageApprover.mockReturnValue(false);

    TestBed.configureTestingModule({
      imports: [
        NavBarComponent
      ],
      schemas: [NO_ERRORS_SCHEMA],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: NotificationsService, useValue: mockNotificationsService },
        { provide: SignalRService, useValue: mockSignalRService },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ reviewId: 'test' }),
            },
            queryParams: of(convertToParamMap({ activeApiRevisionId: 'test', diffApiRevisionId: 'test' }))
          },
        },
        { provide: PermissionsService, useValue: mockPermissionsService },
        { provide: AuthService, useValue: mockAuthService },
        { provide: UserProfileService, useValue: mockUserProfileService },
        { provide: NotificationsService, useValue: mockNotificationsService },
        { provide: ConfigService, useValue: mockConfigService }
      ]
    });
    fixture = TestBed.createComponent(NavBarComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('isAdmin', () => {
    it('should set isAdmin to false when user has no admin role', () => {
      mockPermissionsService.isAdmin.mockReturnValue(false);
      expect(component.isAdmin).toBe(false);
    });

    it('should set isAdmin to true when user has admin role', () => {
      mockPermissionsService.isAdmin.mockReturnValue(true);

      const profileWithPermissions: UserProfile = {
        ...mockUserProfile,
        permissions: mockAdminPermissions
      };
      mockUserProfileService.getUserProfile.mockReturnValue(of(profileWithPermissions));

      // Recreate component to pick up new mock values
      fixture = TestBed.createComponent(NavBarComponent);
      component = fixture.componentInstance;
      fixture.detectChanges();

      expect(component.isAdmin).toBe(true);
    });

    it('should fetch permissions from service when not on userProfile', () => {
      mockPermissionsService.isAdmin.mockReturnValue(false);
      mockPermissionsService.getMyPermissions.mockReturnValue(of(mockPermissions));

      const profileWithoutPermissions: UserProfile = {
        ...mockUserProfile,
        permissions: null
      };
      mockUserProfileService.getUserProfile.mockReturnValue(of(profileWithoutPermissions));

      fixture = TestBed.createComponent(NavBarComponent);
      component = fixture.componentInstance;
      fixture.detectChanges();

      expect(mockPermissionsService.getMyPermissions).toHaveBeenCalled();
    });
  });
});
