import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { MessageService } from 'primeng/api';
import { vi } from 'vitest';
import { initializeTestBed } from '../../../test-setup';
import { NotificationsService } from 'src/app/_services/notifications/notifications.service';
import { SignalRService } from 'src/app/_services/signal-r/signal-r.service';
import { WorkerService } from 'src/app/_services/worker/worker.service';
import { createMockSignalRService, createMockNotificationsService, createMockWorkerService, setupMatchMediaMock } from 'src/test-helpers/mock-services';

// Mock ngx-ui-scroll to avoid vscroll dependency error
vi.mock('ngx-ui-scroll', () => {
  const UiScrollModuleMock = class UiScrollModule {
    static ɵmod = { 
      id: 'UiScrollModule',
      declarations: [],
      imports: [],
      exports: []
    };
    static ɵinj = { 
      imports: [],
      providers: []
    };
  };
  
  return {
    UiScrollModule: UiScrollModuleMock
  };
});

// Mock ngx-simplemde to avoid ESM import error
vi.mock('ngx-simplemde', () => {
  const SimplemdeModuleMock = class SimplemdeModule {
    static ɵmod = { 
      id: 'SimplemdeModule',
      declarations: [],
      imports: [],
      exports: []
    };
    static ɵinj = { 
      imports: [],
      providers: []
    };
    static forRoot() {
      return {
        ngModule: SimplemdeModuleMock,
        providers: []
      };
    }
  };
  
  return {
    SimplemdeModule: SimplemdeModuleMock,
    SimplemdeOptions: class SimplemdeOptions {
      constructor() {}
    },
    SimplemdeComponent: class SimplemdeComponent {
      value = '';
      options = {};
      delay = 0;
      valueChange = { emit: vi.fn() };
    }
  };
});

import { RevisionPageComponent } from './revision-page.component';

describe('RevisionPageComponent', () => {
  let component: RevisionPageComponent;
  let fixture: ComponentFixture<RevisionPageComponent>;

  const mockNotificationsService = createMockNotificationsService();
  const mockSignalRService = createMockSignalRService();
  const mockWorkerService = createMockWorkerService();

  beforeAll(() => {
    initializeTestBed();
    setupMatchMediaMock();
  });

  beforeEach(() => {
    TestBed.configureTestingModule({
      schemas: [NO_ERRORS_SCHEMA],
      imports: [RevisionPageComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        { provide: NotificationsService, useValue: mockNotificationsService },
        { provide: SignalRService, useValue: mockSignalRService },
        { provide: WorkerService, useValue: mockWorkerService },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ reviewId: 'test' })
            }
          }
        },
        MessageService
      ]
    });
    fixture = TestBed.createComponent(RevisionPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});