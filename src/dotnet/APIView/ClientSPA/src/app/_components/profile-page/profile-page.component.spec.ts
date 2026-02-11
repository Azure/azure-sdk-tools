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
import { createMockSignalRService, createMockNotificationsService } from 'src/test-helpers/mock-services';


describe('ProfilePageComponent', () => {
  let component: ProfilePageComponent;
  let fixture: ComponentFixture<ProfilePageComponent>;

  const mockSignalRService = createMockSignalRService();
  const mockNotificationsService = createMockNotificationsService();

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

  it('should NOT render reviewLanguagesListItem when user is not an approver', () => {
    component.userName = 'testuser';
    component.isApprover = false; // User is not an approver
    component.userProfile = { userName: 'testuser', preferences: {}, permissions: null } as any;
    component.isLoaded = true;
    fixture.detectChanges();

    const reviewLanguagesListItem = fixture.debugElement.query(By.css('#reviewLanguagesListItem'));
    expect(reviewLanguagesListItem).toBeNull();
  });

  it('should render reviewLanguagesListItem when user is an approver', () => {
    component.userName = 'testuser';
    component.isApprover = true; // User is an approver
    component.userProfile = { userName: 'testuser', preferences: {}, permissions: { roles: [{ kind: 'scoped', role: 'Architect', language: 'Python' }] } } as any;
    component.isLoaded = true;
    fixture.detectChanges();

    const reviewLanguagesListItem = fixture.debugElement.query(By.css('#reviewLanguagesListItem'));
    expect(reviewLanguagesListItem).toBeTruthy();
  });
});
