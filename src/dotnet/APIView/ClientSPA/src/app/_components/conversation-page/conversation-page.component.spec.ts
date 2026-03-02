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

import { ConversationPageComponent } from './conversation-page.component';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { SignalRService } from 'src/app/_services/signal-r/signal-r.service';
import { NotificationsService } from 'src/app/_services/notifications/notifications.service';
import { WorkerService } from 'src/app/_services/worker/worker.service';
import { ReviewPageLayoutComponent } from '../shared/review-page-layout/review-page-layout.component';
import { ConversationsComponent } from '../conversations/conversations.component';
import { NavBarComponent } from '../shared/nav-bar/nav-bar.component';
import { ReviewInfoComponent } from '../shared/review-info/review-info.component';
import { MenuModule } from 'primeng/menu';
import { MenubarModule } from 'primeng/menubar';
import { TooltipModule } from 'primeng/tooltip';
import { RippleModule } from 'primeng/ripple';
import { LanguageNamesPipe } from 'src/app/_pipes/language-names.pipe';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { DrawerModule } from 'primeng/drawer';
import { 
  createMockSignalRService, 
  createMockNotificationsService, 
  createMockWorkerService,
  setupMatchMediaMock 
} from 'src/test-helpers/mock-services';

describe('ConversationPageComponent', () => {
  let component: ConversationPageComponent;
  let fixture: ComponentFixture<ConversationPageComponent>;

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
        ConversationPageComponent,
        ConversationsComponent,
        NavBarComponent,
        ReviewInfoComponent,
        ReviewPageLayoutComponent,
        LanguageNamesPipe,
        BrowserAnimationsModule,
        MenuModule,
        DrawerModule,
        MenubarModule,
        TooltipModule,
        RippleModule
      ],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: SignalRService, useValue: mockSignalRService },
        { provide: NotificationsService, useValue: mockNotificationsService },
        { provide: WorkerService, useValue: mockWorkerService },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ reviewId: 'test' })
            }
          }
        }
      ]
    });
    fixture = TestBed.createComponent(ConversationPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
