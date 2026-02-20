import 'reflect-metadata';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { initializeTestBed } from '../../../test-setup';

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

// All imports AFTER vi.mock() calls
import { NotificationsService } from 'src/app/_services/notifications/notifications.service';
import { SignalRService } from 'src/app/_services/signal-r/signal-r.service';
import { WorkerService } from 'src/app/_services/worker/worker.service';
import { createMockSignalRService, createMockNotificationsService, createMockWorkerService } from 'src/test-helpers/mock-services';
import { RevisionOptionsComponent } from './revision-options.component';

describe('ApiRevisionOptionsComponent', () => {
  let component: RevisionOptionsComponent;
  let fixture: ComponentFixture<RevisionOptionsComponent>;

  const mockNotificationsService = createMockNotificationsService();
  const mockSignalRService = createMockSignalRService();
  const mockWorkerService = createMockWorkerService();

  beforeAll(() => {
    initializeTestBed();
  });

  beforeEach(() => {
    TestBed.configureTestingModule({
      schemas: [NO_ERRORS_SCHEMA],
      imports: [RevisionOptionsComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: NotificationsService, useValue: mockNotificationsService },
        { provide: SignalRService, useValue: mockSignalRService },
        { provide: WorkerService, useValue: mockWorkerService },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ reviewId: 'test' }),
              queryParamMap: convertToParamMap({ activeApiRevisionId: 'test', diffApiRevisionId: 'test' })
            }
          }
        }
      ]
    });
    fixture = TestBed.createComponent(RevisionOptionsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('Tag APIRevision appropriately based on date and/or status', () => {
    const apiRevisions = [
      {
        id: '1',
        isApproved: false,
        packageVersion: "12.15.1",
        apiRevisionType: 'manual',
      },
      {
        id: '2',
        isApproved: true,
        packageVersion: "12.20.0-beta.2",
        changeHistory: [
          {
            changeAction: 'approved',
            changedOn: '2024-07-01T00:00:00Z',
          }
        ],
        isReleased: true,
        releasedOn: '2024-07-02T00:00:00Z',
        apiRevisionType: 'automatic',
        lastUpdatedOn: '2024-07-01T00:00:00Z',
      },
      {
        id: '3',
        isApproved: true,
        packageVersion: "12.20.0",
        changeHistory: [
          {
            changeAction: 'approved',
            changedOn: '2024-07-04T00:00:00Z',
          }
        ],
        isReleased: true,
        releasedOn: '2024-07-05T00:00:00Z',
        apiRevisionType: 'automatic',
        lastUpdatedOn: '2024-07-04T00:00:00Z',
      },
      {
        id: '4',
        isApproved: true,
        packageVersion: "12.21.1",
        changeHistory: [
          {
            changeAction: 'approved',
            changedOn: '2024-07-05T00:00:00Z',
          }
        ],
        isReleased: false,
        apiRevisionType: 'automatic',
        lastUpdatedOn: '2024-07-05T00:00:00Z',
      },
      {
        id: '5',
        isApproved: false,
        packageVersion: "13.0.0",
        isReleased: false,
        apiRevisionType: 'automatic',
        lastUpdatedOn: '2024-07-04T00:00:00Z',
      },
      {
        id: '6',
        isApproved: true,
        packageVersion: "11.0.0",
        changeHistory: [
          {
            changeAction: 'approved',
            changedOn: '2021-07-04T00:00:00Z',
          }
        ],
        isReleased: true,
        releasedOn: '2021-07-05T00:00:00Z',
        apiRevisionType: 'automatic',
        lastUpdatedOn: '2021-07-04T00:00:00Z',
      },
    ];

    it('should correctly tag the latest GA APIRevision', () => {
      var result = component.tagLatestGARevision(apiRevisions);
      expect(result.id).toEqual('3');
      expect(result.isLatestGA).toBeTruthy();
    });

    it('should correctly tag the latest approved APIRevision', () => {
      var result = component.tagLatestApprovedRevision(apiRevisions);
      expect(result.id).toEqual('4');
      expect(result.isLatestApproved).toBeTruthy();
    });

    it('should correctly tag the latest automatic APIRevision', () => {
      var result = component.tagCurrentMainRevision(apiRevisions);
      expect(result.id).toEqual('4');
      expect(result.isLatestMain).toBeTruthy();
    });

    it('should correctly tag the latest released APIRevision', () => {
      var result = component.tagLatestReleasedRevision(apiRevisions);
      expect(result.id).toEqual('3');
      expect(result.isLatestReleased).toBeTruthy();
    });
  })
});
