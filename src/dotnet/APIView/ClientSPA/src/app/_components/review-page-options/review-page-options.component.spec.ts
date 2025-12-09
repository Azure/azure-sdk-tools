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
import { MessageService } from 'primeng/api';
import { Review } from 'src/app/_models/review';
import { APIRevision } from 'src/app/_models/revision';

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
        },
        MessageService
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
      component.loadingStatus = "completed";
      fixture.detectChanges();
      const button = fixture.nativeElement.querySelector('#first-release-approval-button');
      expect(button).not.toBeTruthy();
      const message : HTMLElement = fixture.nativeElement.querySelector('#first-release-approval-message');
      expect(message.textContent?.startsWith("Approved for first release by:")).toBeTruthy()
    });
    it('should disable first release approval button when review is not approved and user is not an approver', () => {
      component.reviewIsApproved = false;
      component.userProfile = new UserProfile();
      component.userProfile.userName = "test-user-1";
      component.preferredApprovers = ["test-user-2"]
      component.loadingStatus = "completed";
      fixture.detectChanges();
      const button = fixture.nativeElement.querySelector('#first-release-approval-button');
      expect(button).not.toBeTruthy();
      const message : HTMLElement = fixture.nativeElement.querySelector('#first-release-approval-message');
      expect(message.textContent).toEqual("First release approval pending");
    });
    it('should enable first release approval button when review is not approved and user is an approver', () => {
      component.reviewIsApproved = false;
      component.userProfile = new UserProfile();
      component.userProfile.userName = "test-user";
      component.preferredApprovers = ["test-user"]
      component.loadingStatus = "completed";
      fixture.detectChanges();
      const button = fixture.nativeElement.querySelector('#first-release-approval-button');
      expect(button).toBeTruthy();
      const message : HTMLElement = fixture.nativeElement.querySelector('#first-release-approval-message');
      expect(message.textContent).toEqual("First release approval pending");
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
      expect(component.showLineNumbersSwitch).toEqual(true);
      expect(component.disableCodeLinesLazyLoading).toEqual(false);
    })
  });

  describe('Toggle APIRevision Approval', () => {
    it('should close APIRevision Approval Modal', () => {
      component.showAPIRevisionApprovalModal = true;
      component.loadingStatus = "completed";
      fixture.detectChanges();
      component.toggleAPIRevisionApproval();
      expect(component.showAPIRevisionApprovalModal).not.toBeTruthy();
    });
  });

  describe('Copilot Review Support', () => {
    beforeEach(() => {
      // Setup common test data
      component.review = new Review();
      component.activeAPIRevision = new APIRevision();
      component.userProfile = new UserProfile();
      component.userProfile.userName = 'test-user';
    });

    describe('isCopilotReviewSupportedForPackage', () => {
      it('should return true when review or language is undefined', () => {
        component.review = undefined;
        const result = component['isCopilotReviewSupportedForPackage']();
        expect(result).toBe(true);
      });

      it('should return false for @azure-rest JavaScript packages', () => {
        component.review!.packageName = '@azure-rest/ai-document-intelligence';
        component.review!.language = 'JavaScript';
        const result = component['isCopilotReviewSupportedForPackage']();
        expect(result).toBe(false);
      });

      it('should return true for @azure-rest packages in other languages', () => {
        component.review!.packageName = '@azure-rest/ai-document-intelligence';
        component.review!.language = 'TypeScript';
        const result = component['isCopilotReviewSupportedForPackage']();
        expect(result).toBe(true);
      });

      it('should return true for non-@azure-rest JavaScript packages', () => {
        component.review!.packageName = '@azure/storage-blob';
        component.review!.language = 'JavaScript';
        const result = component['isCopilotReviewSupportedForPackage']();
        expect(result).toBe(true);
      });

      it('should return false for TypeSpec packages', () => {
        component.review!.packageName = 'some-package';
        component.review!.language = 'TypeSpec';
        const result = component['isCopilotReviewSupportedForPackage']();
        expect(result).toBe(false);
      });
    });

    describe('isCopilotReviewSupported property', () => {
      it('should be updated when activeAPIRevision changes', () => {
        // Setup for unsupported package
        component.review!.packageName = '@azure-rest/test-package';
        component.review!.language = 'JavaScript';
        
        // Trigger ngOnChanges
        component.ngOnChanges({
          activeAPIRevision: {
            currentValue: component.activeAPIRevision,
            previousValue: undefined,
            firstChange: true,
            isFirstChange: () => true
          }
        });

        expect(component.isCopilotReviewSupported).toBe(false);
      });
    });

    describe('shouldDisableApproval', () => {
      beforeEach(() => {
        component.activeAPIRevision!.packageVersion = '1.0.0';
        component.activeAPIRevision!.language = 'JavaScript';
        component.activeAPIRevision!.approvers = [];
        component.isCopilotReviewSupported = true;
        component.activeAPIRevisionIsApprovedByCurrentUser = false;
      });

      // Test cases that should return FALSE (approval NOT disabled)
      it('should return false when copilot review is not supported for package', () => {
        component.isCopilotReviewSupported = false;
        const isReviewByCopilotRequired = true;
        const isVersionReviewedByCopilot = false;
        const result = component['shouldDisableApproval'](isReviewByCopilotRequired, isVersionReviewedByCopilot);
        expect(result).toBe(false);
      });

      it('should return false for preview versions even when copilot review required', () => {
        component.activeAPIRevision!.packageVersion = '1.0.0-beta.1';
        const isReviewByCopilotRequired = true;
        const isVersionReviewedByCopilot = false;
        const result = component['shouldDisableApproval'](isReviewByCopilotRequired, isVersionReviewedByCopilot);
        expect(result).toBe(false);
      });

      it('should return false when user has already approved', () => {
        component.activeAPIRevision!.approvers = ['test-user'];
        component.activeAPIRevisionIsApprovedByCurrentUser = true;
        const isReviewByCopilotRequired = true;
        const isVersionReviewedByCopilot = false;
        const result = component['shouldDisableApproval'](isReviewByCopilotRequired, isVersionReviewedByCopilot);
        expect(result).toBe(false);
      });

      it('should return false when copilot review required and completed', () => {
        const isReviewByCopilotRequired = true;
        const isVersionReviewedByCopilot = true;
        const result = component['shouldDisableApproval'](isReviewByCopilotRequired, isVersionReviewedByCopilot);
        expect(result).toBe(false);
      });

      it('should return false when copilot review not required', () => {
        const isReviewByCopilotRequired = false;
        const isVersionReviewedByCopilot = false;
        const result = component['shouldDisableApproval'](isReviewByCopilotRequired, isVersionReviewedByCopilot);
        expect(result).toBe(false);
      });
      
      // Test cases that should return TRUE (approval DISABLED)
      it('should return true when copilot review required but not completed', () => {
        const isReviewByCopilotRequired = true;
        const isVersionReviewedByCopilot = false;
        const result = component['shouldDisableApproval'](isReviewByCopilotRequired, isVersionReviewedByCopilot);
        expect(result).toBe(true);
      });

      it('should return true when copilot review supported and user has not approved yet', () => {
        component.isCopilotReviewSupported = true;
        component.activeAPIRevisionIsApprovedByCurrentUser = false;
        const isReviewByCopilotRequired = true;
        const isVersionReviewedByCopilot = false;
        const result = component['shouldDisableApproval'](isReviewByCopilotRequired, isVersionReviewedByCopilot);
        expect(result).toBe(true);
      });

      it('should return true for complex version numbers when copilot review required but not completed', () => {
        component.activeAPIRevision!.packageVersion = '12.5.3';
        const isReviewByCopilotRequired = true;
        const isVersionReviewedByCopilot = false;
        const result = component['shouldDisableApproval'](isReviewByCopilotRequired, isVersionReviewedByCopilot);
        expect(result).toBe(true);
      });

      // Edge cases
      it('should handle invalid package version gracefully - should still disable when copilot required', () => {
        component.activeAPIRevision!.packageVersion = 'invalid-version';
        const isReviewByCopilotRequired = true;
        const isVersionReviewedByCopilot = false;
        const result = component['shouldDisableApproval'](isReviewByCopilotRequired, isVersionReviewedByCopilot);
        expect(result).toBe(true);
      });

      it('should handle empty package version - should still disable when copilot required', () => {
        component.activeAPIRevision!.packageVersion = '';
        const isReviewByCopilotRequired = true;
        const isVersionReviewedByCopilot = false;
        const result = component['shouldDisableApproval'](isReviewByCopilotRequired, isVersionReviewedByCopilot);
        expect(result).toBe(true);
      });
    });

    describe('Integration Tests', () => {
      it('should set correct approval states for unsupported copilot packages', () => {
        component.review!.packageName = '@azure-rest/test-package';
        component.review!.language = 'JavaScript';
        component.activeAPIRevision!.packageVersion = '1.0.0';
        component.activeAPIRevision!.approvers = [];
        
        component.isCopilotReviewSupported = component['isCopilotReviewSupportedForPackage']();
        const isReviewByCopilotRequired = true;
        const isVersionReviewedByCopilot = false;
        const shouldDisable = component['shouldDisableApproval'](isReviewByCopilotRequired, isVersionReviewedByCopilot);
        
        expect(component.isCopilotReviewSupported).toBe(false);
        expect(shouldDisable).toBe(false); // Should not disable because copilot not available
      });

      it('should set correct approval states for supported copilot packages', () => {
        component.review!.packageName = '@azure/storage-blob';
        component.review!.language = 'JavaScript';
        component.activeAPIRevision!.packageVersion = '1.0.0';
        component.activeAPIRevision!.approvers = [];
        
        component.isCopilotReviewSupported = component['isCopilotReviewSupportedForPackage']();
        const isReviewByCopilotRequired = true;
        const isVersionReviewedByCopilot = false;
        const shouldDisable = component['shouldDisableApproval'](isReviewByCopilotRequired, isVersionReviewedByCopilot);
        
        expect(component.isCopilotReviewSupported).toBe(true);
        expect(shouldDisable).toBe(true); // Should disable because copilot required but not completed
      });
    });

    describe('updateApprovalStates', () => {
      beforeEach(() => {
        // Reset to default state for each test
        component.isCopilotReviewSupported = true;
        component.userProfile = { userName: 'testuser' } as UserProfile;
        component.activeAPIRevision = {
          approvers: [],
          packageVersion: '1.0.0'
        } as unknown as APIRevision;
        component.review = {
          language: 'C#',
          packageName: 'Azure.SomePackage'
        } as Review;
        component.canToggleApproveAPIRevision = true;
      });

      it('should not disable approval when copilot review is not supported', () => {
        component.isCopilotReviewSupported = false;
        component.activeAPIRevision!.approvers = [];
        
        component['updateApprovalStates'](true, false);
        
        expect(component.isAPIRevisionApprovalDisabled).toBe(false);
      });

      it('should not disable approval for preview versions', () => {
        component.activeAPIRevision!.packageVersion = '1.0.0-beta.1';
        component.activeAPIRevision!.approvers = [];
        spyOn(component as any, 'isPreviewVersion').and.returnValue(true);
        
        component['updateApprovalStates'](true, false);
        
        expect(component.isAPIRevisionApprovalDisabled).toBe(false);
      });

      it('should not disable approval when user has already approved', () => {
        component.activeAPIRevision!.approvers = ['testuser'];
        
        component['updateApprovalStates'](true, false);
        
        expect(component.isAPIRevisionApprovalDisabled).toBe(false);
      });

      it('should disable approval when copilot review is required but not completed', () => {
        component.activeAPIRevision!.approvers = [];
        
        component['updateApprovalStates'](true, false);
        
        expect(component.isAPIRevisionApprovalDisabled).toBe(true);
      });

      it('should not disable approval when copilot review is completed', () => {
        component.activeAPIRevision!.approvers = [];
        
        component['updateApprovalStates'](true, true);
        
        expect(component.isAPIRevisionApprovalDisabled).toBe(false);
      });

      it('should not disable approval when copilot review is not required', () => {
        component.activeAPIRevision!.approvers = [];
        
        component['updateApprovalStates'](false, false);
        
        expect(component.isAPIRevisionApprovalDisabled).toBe(false);
      });

      it('should handle copilot not supported overriding requirement', () => {
        component.isCopilotReviewSupported = false;
        component.activeAPIRevision!.approvers = [];
        
        component['updateApprovalStates'](true, false);
        
        expect(component.isAPIRevisionApprovalDisabled).toBe(false);
      });

      it('should handle preview version overriding copilot requirement', () => {
        component.activeAPIRevision!.packageVersion = '2.0.0-alpha.3';
        component.activeAPIRevision!.approvers = [];
        spyOn(component as any, 'isPreviewVersion').and.returnValue(true);
        
        component['updateApprovalStates'](true, false);
        
        expect(component.isAPIRevisionApprovalDisabled).toBe(false);
      });

      it('should set correct button classes when approval is disabled', () => {
        component.activeAPIRevision!.approvers = [];
        
        component['updateApprovalStates'](true, false);
        
        expect(component.isAPIRevisionApprovalDisabled).toBe(true);
        expect(component.apiRevisionApprovalBtnClass).toBe("btn btn-outline-secondary disabled");
        expect(component.apiRevisionApprovalMessage).toBe("To approve the current API revision, it must first be reviewed by Copilot");
      });
    });
  });
});
