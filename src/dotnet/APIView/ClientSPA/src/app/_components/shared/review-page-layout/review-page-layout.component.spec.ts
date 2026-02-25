import { ComponentFixture, TestBed } from '@angular/core/testing';
import { initializeTestBed } from '../../../../test-setup';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';

import { ReviewPageLayoutComponent } from './review-page-layout.component';
import { NavBarComponent } from '../nav-bar/nav-bar.component';
import { ReviewInfoComponent } from '../review-info/review-info.component';
import { MenubarModule } from 'primeng/menubar';
import { MenuModule } from 'primeng/menu';
import { LanguageNamesPipe } from 'src/app/_pipes/language-names.pipe';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { DrawerModule } from 'primeng/drawer';
import { NotificationsService } from 'src/app/_services/notifications/notifications.service';
import { SignalRService } from 'src/app/_services/signal-r/signal-r.service';
import { of, Subject } from 'rxjs';
import { vi } from 'vitest';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { createMockSignalRService, createMockNotificationsService, createMockWorkerService } from 'src/test-helpers/mock-services';

describe('ReviewPageLayoutComponent', () => {
  let component: ReviewPageLayoutComponent;
  let fixture: ComponentFixture<ReviewPageLayoutComponent>;

  const mockNotificationsService = createMockNotificationsService();
  const mockSignalRService = createMockSignalRService();

  beforeAll(() => {
    initializeTestBed();
  });

  beforeEach(() => {
    Object.defineProperty(window, 'matchMedia', {
      writable: true,
      value: vi.fn().mockImplementation((query: string) => ({
        matches: false,
        media: query,
        onchange: null,
        addListener: vi.fn(),
        removeListener: vi.fn(),
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
        dispatchEvent: vi.fn()
      }))
    });

    TestBed.configureTestingModule({
      imports: [
        ReviewPageLayoutComponent,
        ReviewInfoComponent,
        NavBarComponent,
        LanguageNamesPipe,
        BrowserAnimationsModule,
        MenubarModule,
        DrawerModule,
        MenuModule
      ],
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
              queryParamMap: convertToParamMap({ activeApiRevisionId: 'test' })
            }
          }
        }
      ],
      schemas: [NO_ERRORS_SCHEMA]
    });
    fixture = TestBed.createComponent(ReviewPageLayoutComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
