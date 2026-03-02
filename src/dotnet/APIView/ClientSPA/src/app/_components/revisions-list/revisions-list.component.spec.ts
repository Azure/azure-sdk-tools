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
import { RevisionsListComponent } from './revisions-list.component';
import { SharedAppModule } from 'src/app/_modules/shared/shared-app.module';
import { AppModule } from 'src/app/app.module';
import { SignalRService } from 'src/app/_services/signal-r/signal-r.service';
import { NotificationsService } from 'src/app/_services/notifications/notifications.service';
import { WorkerService } from 'src/app/_services/worker/worker.service';
import { createMockSignalRService, createMockNotificationsService, createMockWorkerService } from 'src/test-helpers/mock-services';

describe('RevisionListComponent', () => {
  let component: RevisionsListComponent;
  let fixture: ComponentFixture<RevisionsListComponent>;

  const mockSignalRService = createMockSignalRService();
  const mockNotificationsService = createMockNotificationsService();
  const mockWorkerService = createMockWorkerService();

  beforeAll(() => {
    initializeTestBed();
    // Ensure matchMedia is defined for PrimeNG ContextMenu
    if (!window.matchMedia) {
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
    }
  });

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [
        RevisionsListComponent,
        SharedAppModule,
        AppModule
      ],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: SignalRService, useValue: mockSignalRService },
        { provide: NotificationsService, useValue: mockNotificationsService },
        { provide: WorkerService, useValue: mockWorkerService }
      ]
    });
    fixture = TestBed.createComponent(RevisionsListComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should update List Details Message onChage', () => {
    component.showDeletedAPIRevisions = true;
    expect(component.apiRevisionsListDetail).toContain('Deleted APIRevision(s)');
    component.showDeletedAPIRevisions = false;
    expect(component.apiRevisionsListDetail).not.toContain('Deleted APIRevision(s)')
    component.showAPIRevisionsAssignedToMe = true;
    expect(component.apiRevisionsListDetail).toContain('Assigned to Me');
    component.showAPIRevisionsAssignedToMe = false;
    expect(component.apiRevisionsListDetail).not.toContain('Assigned to Me')
  });
});
