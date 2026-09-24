import { vi } from 'vitest';
vi.mock('ngx-simplemde', () => ({
  SimplemdeModule: class {
    static ɵmod = { id: 'SimplemdeModule', type: this, declarations: [], imports: [], exports: [] };
    static ɵinj = { imports: [], providers: [] };
    static forRoot() { return { ngModule: this, providers: [] }; }
  },
  SimplemdeOptions: class {},
  SimplemdeComponent: class { value = ''; options = {}; valueChange = { emit: vi.fn() }; }
}));
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { initializeTestBed } from '../../../test-setup';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { MessageService } from 'primeng/api';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';

import { NamespacePageComponent } from './namespace-page.component';
import { SignalRService } from 'src/app/_services/signal-r/signal-r.service';
import { NotificationsService } from 'src/app/_services/notifications/notifications.service';
import { WorkerService } from 'src/app/_services/worker/worker.service';
import { 
  createMockSignalRService, 
  createMockNotificationsService, 
  createMockWorkerService,
  setupMatchMediaMock 
} from 'src/test-helpers/mock-services';

describe('NamespacePageComponent', () => {
  let component: NamespacePageComponent;
  let fixture: ComponentFixture<NamespacePageComponent>;

  const mockSignalRService = createMockSignalRService();
  const mockNotificationsService = createMockNotificationsService();
  const mockWorkerService = createMockWorkerService();

  beforeAll(() => {
    initializeTestBed();
    setupMatchMediaMock();
  });

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [
        NamespacePageComponent,
        BrowserAnimationsModule
      ],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        MessageService,
        { provide: SignalRService, useValue: mockSignalRService },
        { provide: NotificationsService, useValue: mockNotificationsService },
        { provide: WorkerService, useValue: mockWorkerService },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ reviewId: 'test' }),
              queryParamMap: convertToParamMap({ activeApiRevisionId: 'test' })
            }
          }
        }
      ]
    });

    fixture = TestBed.createComponent(NamespacePageComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('getStatusClass', () => {
    it('should return correct class for Approved status', () => {
      expect(component.getStatusClass('Approved' as any)).toBe('status-approved');
    });

    it('should return correct class for Proposed status', () => {
      expect(component.getStatusClass('Proposed' as any)).toBe('status-proposed');
    });
  });

  describe('getStatusLabel', () => {
    it('should return "Approved" for Approved status', () => {
      expect(component.getStatusLabel('Approved' as any)).toBe('Approved');
    });

    it('should return "Proposed" for Proposed status', () => {
      expect(component.getStatusLabel('Proposed' as any)).toBe('Proposed');
    });

    it('should return "Rejected" for Rejected status', () => {
      expect(component.getStatusLabel('Rejected' as any)).toBe('Rejected');
    });
  });
});
