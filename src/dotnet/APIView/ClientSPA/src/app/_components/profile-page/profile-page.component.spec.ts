import { vi } from 'vitest';
vi.mock('ngx-simplemde', () => ({
  SimplemdeModule: class {
    static ɵmod = { id: 'SimplemdeModule', type: this, declarations: [], imports: [], exports: [] };
    static ɵinj = { imports: [], providers: [] };
    static forRoot() { return { ngModule: this, providers: [] }; }
  },
  SimplemdeOptions: class { },
  SimplemdeComponent: class { value = ''; options = {}; valueChange = { emit: vi.fn() }; }
}));
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { initializeTestBed } from '../../../test-setup';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';

import { ProfilePageComponent } from './profile-page.component';
import { By } from '@angular/platform-browser';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { SharedAppModule } from 'src/app/_modules/shared/shared-app.module';
import { SignalRService } from 'src/app/_services/signal-r/signal-r.service';
import { NotificationsService } from 'src/app/_services/notifications/notifications.service';
import { PermissionsService } from 'src/app/_services/permissions/permissions.service';
import { createMockSignalRService, createMockNotificationsService } from 'src/test-helpers/mock-services';
import { of } from 'rxjs';


describe('ProfilePageComponent', () => {
  let component: ProfilePageComponent;
  let fixture: ComponentFixture<ProfilePageComponent>;

  const mockSignalRService = createMockSignalRService();
  const mockNotificationsService = createMockNotificationsService();
  const mockPermissionsService = {
    isLanguageApprover: vi.fn().mockReturnValue(false),
    isAdmin: vi.fn().mockReturnValue(false),
    getApprovableLanguages: vi.fn().mockReturnValue([]),
    getMyGroups: vi.fn().mockReturnValue(of([])),
    getAdminUsernames: vi.fn().mockReturnValue(of([]))
  };

  beforeAll(() => {
    initializeTestBed();
  });

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ProfilePageComponent],
      imports: [
        SharedAppModule
      ],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: SignalRService, useValue: mockSignalRService },
        { provide: NotificationsService, useValue: mockNotificationsService },
        { provide: PermissionsService, useValue: mockPermissionsService },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ userNme: 'test' }),
            }
          }
        }
      ]
    })
      .compileComponents();
    fixture = TestBed.createComponent(ProfilePageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should render permissionsListItem', () => {
    component.userName = 'testuser';
    component.userProfile = { userName: 'testuser', preferences: {}, permissions: null } as any;
    component.isLoaded = true;
    fixture.detectChanges();

    const permissionsListItem = fixture.debugElement.query(By.css('#permissionsListItem'));
    expect(permissionsListItem).toBeTruthy();
  });

  it('should render admin contact info when adminUsernames is populated', () => {
    component.userName = 'testuser';
    component.userProfile = { userName: 'testuser', preferences: {}, permissions: null } as any;
    component.isLoaded = true;
    component.adminUsernames = ['admin1', 'admin2'];
    fixture.detectChanges();

    const permissionsListItem = fixture.debugElement.query(By.css('#permissionsListItem'));
    expect(permissionsListItem).toBeTruthy();
    expect(permissionsListItem.nativeElement.textContent).toContain('admin1');
    expect(permissionsListItem.nativeElement.textContent).toContain('admin2');
  });
});
