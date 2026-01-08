import { ComponentFixture, TestBed } from '@angular/core/testing';

import { NavBarComponent } from './nav-bar.component';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { of } from 'rxjs';
import { PermissionsService } from 'src/app/_services/permissions/permissions.service';
import { AuthService } from 'src/app/_services/auth/auth.service';
import { UserProfileService } from 'src/app/_services/user-profile/user-profile.service';
import { ConfigService } from 'src/app/_services/config/config.service';
import { NotificationsService } from 'src/app/_services/notifications/notifications.service';
import { EffectivePermissions, GlobalRole, GlobalRoleAssignment } from 'src/app/_models/permissions';
import { UserProfile } from 'src/app/_models/userProfile';
import { UserPreferenceModel } from 'src/app/_models/userPreferenceModel';

describe('NavBarComponent', () => {
  let component: NavBarComponent;
  let fixture: ComponentFixture<NavBarComponent>;
  let mockPermissionsService: jasmine.SpyObj<PermissionsService>;
  let mockAuthService: jasmine.SpyObj<AuthService>;
  let mockUserProfileService: jasmine.SpyObj<UserProfileService>;
  let mockNotificationsService: jasmine.SpyObj<NotificationsService>;
  let mockConfigService: jasmine.SpyObj<ConfigService>;

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

  beforeEach(() => {
    mockPermissionsService = jasmine.createSpyObj('PermissionsService', ['getMyPermissions', 'isAdmin']);
    mockAuthService = jasmine.createSpyObj('AuthService', ['isLoggedIn']);
    mockUserProfileService = jasmine.createSpyObj('UserProfileService', ['getUserProfile', 'updateUserProfile']);
    mockNotificationsService = jasmine.createSpyObj('NotificationsService', ['clearNotification', 'clearAll'], {
      notifications$: of([])
    });
    mockConfigService = jasmine.createSpyObj('ConfigService', [], {
      webAppUrl: 'http://localhost/',
      apiUrl: '/api'
    });

    mockAuthService.isLoggedIn.and.returnValue(of(true));
    mockUserProfileService.getUserProfile.and.returnValue(of(mockUserProfile));
    mockPermissionsService.getMyPermissions.and.returnValue(of(mockPermissions));
    mockPermissionsService.isAdmin.and.returnValue(false);

    TestBed.configureTestingModule({
      declarations: [NavBarComponent],
      imports: [HttpClientTestingModule],
      schemas: [NO_ERRORS_SCHEMA],
      providers: [
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
      mockPermissionsService.isAdmin.and.returnValue(false);
      expect(component.isAdmin).toBeFalse();
    });

    it('should set isAdmin to true when user has admin role', () => {
      mockPermissionsService.isAdmin.and.returnValue(true);

      const profileWithPermissions: UserProfile = {
        ...mockUserProfile,
        permissions: mockAdminPermissions
      };
      mockUserProfileService.getUserProfile.and.returnValue(of(profileWithPermissions));

      // Recreate component to pick up new mock values
      fixture = TestBed.createComponent(NavBarComponent);
      component = fixture.componentInstance;
      fixture.detectChanges();

      expect(component.isAdmin).toBeTrue();
    });

    it('should fetch permissions from service when not on userProfile', () => {
      mockPermissionsService.isAdmin.and.returnValue(false);
      mockPermissionsService.getMyPermissions.and.returnValue(of(mockPermissions));

      const profileWithoutPermissions: UserProfile = {
        ...mockUserProfile,
        permissions: null
      };
      mockUserProfileService.getUserProfile.and.returnValue(of(profileWithoutPermissions));

      fixture = TestBed.createComponent(NavBarComponent);
      component = fixture.componentInstance;
      fixture.detectChanges();

      expect(mockPermissionsService.getMyPermissions).toHaveBeenCalled();
    });
  });
});
