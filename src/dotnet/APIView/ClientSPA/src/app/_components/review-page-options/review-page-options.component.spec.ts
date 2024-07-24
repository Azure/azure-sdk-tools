import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ReviewPageOptionsComponent } from './review-page-options.component';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { HTTP_INTERCEPTORS, HttpClientModule } from '@angular/common/http';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { HttpErrorInterceptorService } from 'src/app/_services/http-error-interceptor/http-error-interceptor.service';
import { PageOptionsSectionComponent } from '../shared/page-options-section/page-options-section.component';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { SharedAppModule } from 'src/app/_modules/shared/shared-app.module';
import { ReviewPageModule } from 'src/app/_modules/review-page/review-page.module';
import { UserProfile } from 'src/app/_models/userProfile';
import { Review } from 'src/app/_models/review';
import { APIRevision } from 'src/app/_models/revision';
import { By } from '@angular/platform-browser';

describe('ReviewPageOptionsComponent', () => {
  let component: ReviewPageOptionsComponent;
  let fixture: ComponentFixture<ReviewPageOptionsComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      declarations: [
        ReviewPageOptionsComponent,
        PageOptionsSectionComponent
      ],
      imports: [
        HttpClientTestingModule,,
        HttpClientModule,
        BrowserAnimationsModule,
        SharedAppModule,
        ReviewPageModule
      ],
      providers: [
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ reviewId: 'test' }),
              queryParamMap: convertToParamMap({ activeApiRevisionId: 'test', diffApiRevisionId: 'test' })
            }
          }
        },
        {
          provide: HTTP_INTERCEPTORS,
          useClass: HttpErrorInterceptorService,
          multi: true
        }
      ]
    });
    fixture = TestBed.createComponent(ReviewPageOptionsComponent);
    component = fixture.componentInstance;

    // initialize component properties
    component.userProfile = new UserProfile();
    component.review = new Review();
    component.diffStyleInput = 'Full Diff';
    component.activeAPIRevision = new APIRevision();
    component.diffAPIRevision = new APIRevision();
    component.canApproveReview = false;
    component.reviewIsApproved = false;


    @Input() userProfile: UserProfile | undefined;
    @Input() isDiffView: boolean = false;
    @Input() diffStyleInput: string | undefined;
    @Input() review : Review | undefined = undefined;
    @Input() activeAPIRevision : APIRevision | undefined = undefined;
    @Input() diffAPIRevision : APIRevision | undefined = undefined;
    @Input() preferredApprovers: string[] = [];
    @Input() hasFatalDiagnostics : boolean = false;
    @Input() hasActiveConversation : boolean = false;
    @Input() hasHiddenAPIs : boolean = false;

    canToggleApproveAPIRevision: boolean = false;
    activeAPIRevisionIsApprovedByCurrentUser: boolean = false;
    apiRevisionApprovalMessage: string = '';
    apiRevisionApprovalBtnClass: string = '';
    apiRevisionApprovalBtnLabel: string = '';
    showAPIRevisionApprovalModal: boolean = false;
    overrideActiveConversationforApproval : boolean = false;
    overrideFatalDiagnosticsforApproval : boolean = false;
    
    canApproveReview: boolean | undefined = undefined;
    reviewIsApproved: boolean | undefined = undefined;

    // Initialize child components
    const childComponentDE = fixture.debugElement.query(By.directive(PageOptionsSectionComponent));
    const childComponent = childComponentDE.componentInstance;

    childComponent.sectionName = 'Test Section';
    childComponent.collapsedInput = false
    childComponent.sectionId = 'Test Id'
    childComponent.collapsed = false;
    childComponent.sectionStateCookieKey = 'Test Key';

    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
