import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { MessageService } from 'primeng/api';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { initializeTestBed } from '../../../test-setup';
import { NotificationsService } from 'src/app/_services/notifications/notifications.service';
import { SignalRService } from 'src/app/_services/signal-r/signal-r.service';
import { createMockSignalRService, createMockNotificationsService } from 'src/test-helpers/mock-services';

// Mock ngx-simplemde to prevent UMD import failure in test environment
vi.mock('ngx-simplemde', () => ({
  SimplemdeModule: class {
    static forRoot() { return { ngModule: this, providers: [] }; }
  },
  SimplemdeOptions: class {},
  SimplemdeComponent: class {}
}));

import { SamplesPageComponent } from './samples-page.component';

describe('SamplesPageComponent', () => {
  let component: SamplesPageComponent;
  let fixture: ComponentFixture<SamplesPageComponent>;

  const mockNotificationsService = createMockNotificationsService();
  const mockSignalRService = createMockSignalRService();

  beforeAll(() => {
    initializeTestBed();
  });

  beforeEach(() => {
    TestBed.configureTestingModule({
      schemas: [NO_ERRORS_SCHEMA],
      imports: [SamplesPageComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        { provide: NotificationsService, useValue: mockNotificationsService },
        { provide: SignalRService, useValue: mockSignalRService },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ reviewId: 'test' }),
            },
            queryParams: of(convertToParamMap({ activeSamplesRevisionId: 'test' }))
          },
        },
        MessageService
      ]
    });
    TestBed.overrideComponent(SamplesPageComponent, {
      set: { imports: [], schemas: [NO_ERRORS_SCHEMA] }
    });
    fixture = TestBed.createComponent(SamplesPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
