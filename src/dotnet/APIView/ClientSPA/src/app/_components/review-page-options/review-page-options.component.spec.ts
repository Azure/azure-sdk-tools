import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ReviewPageOptionsComponent } from './review-page-options.component';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { HTTP_INTERCEPTORS, HttpClientModule } from '@angular/common/http';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { HttpErrorInterceptorService } from 'src/app/_services/http-error-interceptor/http-error-interceptor.service';
import { PageOptionsSectionComponent } from '../shared/page-options-section/page-options-section.component';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { SharedAppModule } from 'src/app/_modules/shared/shared-app.module';
import { ReviewPageModule } from 'src/app/_modules/review-page.module';
import { UserProfile } from 'src/app/_models/userProfile';

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
        HttpClientTestingModule,
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
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('First Release Approval Button', () => {
    it('should disable first release approval button when review is approved', () => {
      component.reviewIsApproved = true;
      fixture.detectChanges();
      const button = fixture.nativeElement.querySelector('#first-release-approval-button');
      expect(button).not.toBeTruthy();
      const message : HTMLElement = fixture.nativeElement.querySelector('#first-release-approval-message');
      expect(message.textContent?.startsWith("Approved for First Release By:")).toBeTruthy()
    });
    it('should disable first release approval button when review is not approved and user is not an approver', () => {
      component.reviewIsApproved = false;
      component.userProfile = new UserProfile();
      component.userProfile.userName = "test-user-1";
      component.preferredApprovers = ["test-user-2"]
      fixture.detectChanges();
      const button = fixture.nativeElement.querySelector('#first-release-approval-button');
      expect(button).not.toBeTruthy();
      const message : HTMLElement = fixture.nativeElement.querySelector('#first-release-approval-message');
      expect(message.textContent).toEqual("First Release Approval Pending");
    });
    it('should enable first release approval button when review is not approved and user is an approver', () => {
      component.reviewIsApproved = false;
      component.userProfile = new UserProfile();
      component.userProfile.userName = "test-user";
      component.preferredApprovers = ["test-user"]
      fixture.detectChanges();
      const button = fixture.nativeElement.querySelector('#first-release-approval-button');
      expect(button).toBeTruthy();
      const message : HTMLElement = fixture.nativeElement.querySelector('#first-release-approval-message');
      expect(message.textContent).toEqual("First Release Approval Pending");
    });
  });

  describe('Page Option Values', () => {
    it('Should set Page Option Defaults when UserProfile is undefined', () => {
      component.userProfile = undefined;
      component.ngOnInit();
      expect(component.userProfile).toBeUndefined();
      expect(component.showCommentsSwitch).toEqual(true);
      expect(component.showSystemCommentsSwitch).toEqual(true);
      expect(component.showDocumentationSwitch).toEqual(true);
      expect(component.showHiddenAPISwitch).toEqual(false);
      expect(component.showLeftNavigationSwitch).toEqual(true);
      expect(component.markedAsViewSwitch).toEqual(false);
      expect(component.showLineNumbersSwitch).toEqual(true);
      expect(component.disableCodeLinesLazyLoading).toEqual(false);
    })
  });
});
