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
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ReviewPageOptionsComponent, ApprovalDisabledReason } from './review-page-options.component';
import { HttpClientModule, HTTP_INTERCEPTORS } from '@angular/common/http';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { HttpErrorInterceptorService } from 'src/app/_services/http-error-interceptor/http-error-interceptor.service';
import { PageOptionsSectionComponent } from '../shared/page-options-section/page-options-section.component';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { SharedAppModule } from 'src/app/_modules/shared/shared-app.module';
import { ReviewPageModule } from 'src/app/_modules/review-page.module';
import { UserProfile } from 'src/app/_models/userProfile';
import { MessageService, ConfirmationService } from 'primeng/api';
import { Review } from 'src/app/_models/review';
import { APIRevision } from 'src/app/_models/revision';
import { SignalRService } from 'src/app/_services/signal-r/signal-r.service';
import { NotificationsService } from 'src/app/_services/notifications/notifications.service';
import { PermissionsService } from 'src/app/_services/permissions/permissions.service';
import { ReviewContextService } from 'src/app/_services/review-context/review-context.service';
import { EffectivePermissions, GlobalRole, LanguageScopedRole } from 'src/app/_models/permissions';
import { of } from 'rxjs';

import { createMockSignalRService, createMockNotificationsService, createMockWorkerService } from 'src/test-helpers/mock-services';

describe('ReviewPageOptionsComponent', () => {
  let component: ReviewPageOptionsComponent;
  let fixture: ComponentFixture<ReviewPageOptionsComponent>;
  let mockPermissionsService: any;
  let mockReviewContextService: any;

  const mockSignalRService = createMockSignalRService();

  const mockNotificationsService = createMockNotificationsService();

  const mockApproverPermissions: EffectivePermissions = {
    userId: 'test-user',
    roles: [{ kind: 'scoped', role: LanguageScopedRole.Architect, language: 'Python' }]
  };

  beforeAll(() => {
    initializeTestBed();
  });

  beforeEach(() => {
    mockPermissionsService = {
      isApproverFor: vi.fn().mockReturnValue(false),
      isAdmin: vi.fn().mockReturnValue(false)
    };

    mockReviewContextService = {
      getLanguage: vi.fn().mockReturnValue('Python'),
      getLanguageApprovers: vi.fn().mockReturnValue([]),
      getLanguageApprovers$: vi.fn().mockReturnValue(of([]))
    };

    TestBed.configureTestingModule({
      declarations: [
        ReviewPageOptionsComponent
      ],
      imports: [
        PageOptionsSectionComponent,
        HttpClientModule,
        BrowserAnimationsModule,
        SharedAppModule,
        ReviewPageModule
      ],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: SignalRService, useValue: mockSignalRService },
        { provide: NotificationsService, useValue: mockNotificationsService },
        { provide: PermissionsService, useValue: mockPermissionsService },
        { provide: ReviewContextService, useValue: mockReviewContextService },
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
        MessageService,
        ConfirmationService
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
      mockPermissionsService.isApproverFor.mockReturnValue(false);
      component.reviewIsApproved = false;
      component.review = { language: 'Python' } as Review;
      component.userProfile = new UserProfile();
      component.userProfile.userName = "test-user-1";
      component.userProfile.permissions = { userId: 'test-user-1', roles: [] };
      component.loadingStatus = "completed";
      fixture.detectChanges();
      const button = fixture.nativeElement.querySelector('#first-release-approval-button');
      expect(button).not.toBeTruthy();
      const message : HTMLElement = fixture.nativeElement.querySelector('#first-release-approval-message');
      expect(message.textContent).toEqual("First release approval pending");
    });
    it('should enable first release approval button when review is not approved and user is an approver', () => {
      mockPermissionsService.isApproverFor.mockReturnValue(true);
      component.reviewIsApproved = false;
      component.review = { language: 'Python' } as Review;
      component.userProfile = new UserProfile();
      component.userProfile.userName = "test-user";
      component.userProfile.permissions = mockApproverPermissions;
      component.loadingStatus = "completed";
      fixture.detectChanges();
      const button = fixture.nativeElement.querySelector('#first-release-approval-button');
      expect(button).toBeTruthy();
      const message : HTMLElement = fixture.nativeElement.querySelector('#first-release-approval-message');
      expect(message.textContent).toEqual("First release approval pending");
    });
    it('should emit reviewApprovalEmitter when first release approval button is clicked', () => {
      mockPermissionsService.isApproverFor.mockReturnValue(true);
      component.reviewIsApproved = false;
      component.review = { language: 'Python' } as Review;
      component.userProfile = new UserProfile();
      component.userProfile.userName = "test-user";
      component.userProfile.permissions = mockApproverPermissions;
      component.loadingStatus = "completed";
      fixture.detectChanges();
      const emitSpy = vi.spyOn(component.reviewApprovalEmitter, 'emit');
      const button: HTMLButtonElement = fixture.nativeElement.querySelector('#first-release-approval-button');
      expect(button).toBeTruthy();
      button.click();
      expect(emitSpy).toHaveBeenCalledWith(true);
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
      expect(component.showLineNumbersSwitch).toEqual(true);
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

    it('should show approval modal when only diagnostic must fix comments remain', () => {
      component.isAPIRevisionApprovalDisabled = false;
      component.activeAPIRevisionIsApprovedByCurrentUser = false;
      component.hasActiveConversation = false;
      component.hasFatalDiagnostics = false;
      component.qualityScore = {
        score: 80,
        unresolvedMustFixCount: 1,
        unresolvedMustFixDiagnostics: 1,
        unresolvedShouldFixCount: 0,
        unresolvedSuggestionCount: 0,
        unresolvedQuestionCount: 0,
        unresolvedUnknownCount: 0,
        totalUnresolvedCount: 1
      };
      component.hasDiagnosticMustFixApprovalWarning = true;

      component.handleAPIRevisionApprovalAction();

      expect(component.showAPIRevisionApprovalModal).toBe(true);
    });

    it('should approve directly when no approval warning conditions apply', () => {
      component.isAPIRevisionApprovalDisabled = false;
      component.activeAPIRevisionIsApprovedByCurrentUser = false;
      component.hasActiveConversation = false;
      component.hasFatalDiagnostics = false;
      component.qualityScore = {
        score: 100,
        unresolvedMustFixCount: 0,
        unresolvedMustFixDiagnostics: 0,
        unresolvedShouldFixCount: 0,
        unresolvedSuggestionCount: 0,
        unresolvedQuestionCount: 0,
        unresolvedUnknownCount: 0,
        totalUnresolvedCount: 0
      };
      const emitSpy = vi.spyOn(component.apiRevisionApprovalEmitter, 'emit');

      component.handleAPIRevisionApprovalAction();

      expect(component.showAPIRevisionApprovalModal).toBe(false);
      expect(emitSpy).toHaveBeenCalledWith(true);
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

    describe('getApprovalDisabledReasons', () => {
      beforeEach(() => {
        component.activeAPIRevision!.packageVersion = '1.0.0';
        component.activeAPIRevision!.language = 'JavaScript';
        component.activeAPIRevision!.approvers = [];
        component.isCopilotReviewSupported = true;
        component.activeAPIRevisionIsApprovedByCurrentUser = false;
        component.isMissingPackageVersion = false;
      });

      // Test cases that should return empty array (approval NOT disabled)
      it('should return empty array when copilot review is not supported for package', () => {
        component.isCopilotReviewSupported = false;
        const result = component['getApprovalDisabledReasons'](true, false);
        expect(result).toEqual([]);
      });

      it('should return empty array for preview versions even when copilot review required', () => {
        component.activeAPIRevision!.packageVersion = '1.0.0-beta.1';
        const result = component['getApprovalDisabledReasons'](true, false);
        expect(result).toEqual([]);
      });

      it('should return empty array when revision is already approved', () => {
        component.activeAPIRevision!.approvers = ['test-user'];
        component.activeAPIRevision!.isApproved = true;
        const result = component['getApprovalDisabledReasons'](true, false);
        expect(result).toEqual([]);
      });

      it('should return empty array when copilot review required and completed', () => {
        const result = component['getApprovalDisabledReasons'](true, true);
        expect(result).toEqual([]);
      });

      it('should return empty array when copilot review not required', () => {
        const result = component['getApprovalDisabledReasons'](false, false);
        expect(result).toEqual([]);
      });

      // Test cases that should return specific reasons (approval DISABLED)
      it('should return [CopilotReviewRequired] when copilot review required but not completed', () => {
        const result = component['getApprovalDisabledReasons'](true, false);
        expect(result).toEqual([ApprovalDisabledReason.CopilotReviewRequired]);
      });

      it('should return [CopilotReviewRequired] when copilot review supported and user has not approved yet', () => {
        component.isCopilotReviewSupported = true;
        component.activeAPIRevisionIsApprovedByCurrentUser = false;
        const result = component['getApprovalDisabledReasons'](true, false);
        expect(result).toEqual([ApprovalDisabledReason.CopilotReviewRequired]);
      });

      it('should return [CopilotReviewRequired] for complex version numbers when copilot review required but not completed', () => {
        component.activeAPIRevision!.packageVersion = '12.5.3';
        const result = component['getApprovalDisabledReasons'](true, false);
        expect(result).toEqual([ApprovalDisabledReason.CopilotReviewRequired]);
      });

      // Edge cases
      it('should return [CopilotReviewRequired] for invalid package version when copilot required', () => {
        component.activeAPIRevision!.packageVersion = 'invalid-version';
        const result = component['getApprovalDisabledReasons'](true, false);
        expect(result).toEqual([ApprovalDisabledReason.CopilotReviewRequired]);
      });

      it('should include MissingPackageVersion and CopilotReviewRequired when both apply', () => {
        component.activeAPIRevision!.packageVersion = '';
        component.isMissingPackageVersion = true;
        const result = component['getApprovalDisabledReasons'](true, false);
        expect(result).toContain(ApprovalDisabledReason.MissingPackageVersion);
        expect(result).toContain(ApprovalDisabledReason.CopilotReviewRequired);
      });

      it('should return [MissingPackageVersion] when package version is missing (empty string)', () => {
        component.isMissingPackageVersion = true;
        const result = component['getApprovalDisabledReasons'](false, false);
        expect(result).toEqual([ApprovalDisabledReason.MissingPackageVersion]);
      });

      it('should return [MissingPackageVersion] when package version is missing even when copilot review is completed', () => {
        component.isMissingPackageVersion = true;
        const result = component['getApprovalDisabledReasons'](true, true);
        expect(result).toEqual([ApprovalDisabledReason.MissingPackageVersion]);
      });

      it('should return [MissingPackageVersion] when package version is missing even if user already approved', () => {
        component.isMissingPackageVersion = true;
        component.activeAPIRevisionIsApprovedByCurrentUser = true;
        const result = component['getApprovalDisabledReasons'](false, false);
        expect(result).toEqual([ApprovalDisabledReason.MissingPackageVersion]);
      });

      it('should return [UnresolvedMustFix] when there are unresolved must fix comments', () => {
        component.qualityScore = { score: 50, unresolvedMustFixCount: 2, unresolvedShouldFixCount: 0, unresolvedSuggestionCount: 0, unresolvedQuestionCount: 0, unresolvedUnknownCount: 0, totalUnresolvedCount: 2 };
        component.unresolvedMustFixCount = 2;
        const result = component['getApprovalDisabledReasons'](false, false);
        expect(result).toEqual([ApprovalDisabledReason.UnresolvedMustFix]);
      });

      it('should return [CopilotReviewRequired, UnresolvedMustFix] when both apply', () => {
        component.qualityScore = { score: 50, unresolvedMustFixCount: 3, unresolvedShouldFixCount: 0, unresolvedSuggestionCount: 0, unresolvedQuestionCount: 0, unresolvedUnknownCount: 0, totalUnresolvedCount: 3 };
        component.unresolvedMustFixCount = 3;
        const result = component['getApprovalDisabledReasons'](true, false);
        expect(result).toEqual([ApprovalDisabledReason.CopilotReviewRequired, ApprovalDisabledReason.UnresolvedMustFix]);
      });

      it('should return [UnresolvedMustFix] when copilot review is completed but must fix remain', () => {
        component.qualityScore = { score: 50, unresolvedMustFixCount: 1, unresolvedShouldFixCount: 0, unresolvedSuggestionCount: 0, unresolvedQuestionCount: 0, unresolvedUnknownCount: 0, totalUnresolvedCount: 1 };
        component.unresolvedMustFixCount = 1;
        const result = component['getApprovalDisabledReasons'](true, true);
        expect(result).toEqual([ApprovalDisabledReason.UnresolvedMustFix]);
      });

      it('should return [] when only diagnostic must fix comments remain', () => {
        component.qualityScore = {
          score: 80,
          unresolvedMustFixCount: 1,
          unresolvedMustFixDiagnostics: 1,
          unresolvedShouldFixCount: 0,
          unresolvedSuggestionCount: 0,
          unresolvedQuestionCount: 0,
          unresolvedUnknownCount: 0,
          totalUnresolvedCount: 1
        };
        component.unresolvedMustFixCount = component.qualityScore.unresolvedMustFixCount - (component.qualityScore.unresolvedMustFixDiagnostics ?? 0);
        const result = component['getApprovalDisabledReasons'](false, false);
        expect(result).toEqual([]);
      });

      it('should return [UnresolvedMustFix] when copilot is not supported but must fix remain', () => {
        component.isCopilotReviewSupported = false;
        component.qualityScore = { score: 50, unresolvedMustFixCount: 1, unresolvedShouldFixCount: 0, unresolvedSuggestionCount: 0, unresolvedQuestionCount: 0, unresolvedUnknownCount: 0, totalUnresolvedCount: 1 };
        component.unresolvedMustFixCount = 1;
        const result = component['getApprovalDisabledReasons'](true, false);
        expect(result).toEqual([ApprovalDisabledReason.UnresolvedMustFix]);
      });
    });

    describe('Integration Tests', () => {
      it('should set correct approval states for unsupported copilot packages', () => {
        component.review!.packageName = '@azure-rest/test-package';
        component.review!.language = 'JavaScript';
        component.activeAPIRevision!.packageVersion = '1.0.0';
        component.activeAPIRevision!.approvers = [];

        component.isCopilotReviewSupported = component['isCopilotReviewSupportedForPackage']();
        const reason = component['getApprovalDisabledReasons'](true, false);

        expect(component.isCopilotReviewSupported).toBe(false);
        expect(reason).toEqual([]);
      });

      it('should set correct approval states for supported copilot packages', () => {
        component.review!.packageName = '@azure/storage-blob';
        component.review!.language = 'JavaScript';
        component.activeAPIRevision!.packageVersion = '1.0.0';
        component.activeAPIRevision!.approvers = [];

        component.isCopilotReviewSupported = component['isCopilotReviewSupportedForPackage']();
        const reason = component['getApprovalDisabledReasons'](true, false);

        expect(component.isCopilotReviewSupported).toBe(true);
        expect(reason).toEqual([ApprovalDisabledReason.CopilotReviewRequired]);
      });
    });

    describe('updateApprovalStates', () => {
      beforeEach(() => {
        // Reset to default state for each test
        component.isCopilotReviewSupported = true;
        component.isMissingPackageVersion = false;
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
        vi.spyOn(component as any, 'isPreviewVersion').mockReturnValue(true);

        component['updateApprovalStates'](true, false);

        expect(component.isAPIRevisionApprovalDisabled).toBe(false);
      });

      it('should not disable approval when revision is already approved', () => {
        component.activeAPIRevision!.approvers = ['testuser'];
        component.activeAPIRevision!.isApproved = true;

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
        vi.spyOn(component as any, 'isPreviewVersion').mockReturnValue(true);

        component['updateApprovalStates'](true, false);

        expect(component.isAPIRevisionApprovalDisabled).toBe(false);
      });

      it('should set correct button classes when approval is disabled', () => {
        component.activeAPIRevision!.approvers = [];

        component['updateApprovalStates'](true, false);

        expect(component.isAPIRevisionApprovalDisabled).toBe(true);
        expect(component.apiRevisionApprovalBtnClass).toBe("btn btn-outline-secondary disabled");
        expect(component.apiRevisionApprovalMessages).toEqual(["Copilot review must be completed before approving."]);
      });

      it('should show missing version message when package version is missing', () => {
        component.activeAPIRevision!.approvers = [];
        component.isMissingPackageVersion = true;

        component['updateApprovalStates'](false, false);

        expect(component.isAPIRevisionApprovalDisabled).toBe(true);
        expect(component.apiRevisionApprovalBtnClass).toBe("btn btn-outline-secondary disabled");
        expect(component.apiRevisionApprovalMessages).toEqual(["A package version must be set before approving."]);
      });

      it('should show combined message when version is missing and copilot review required', () => {
        component.activeAPIRevision!.approvers = [];
        component.isMissingPackageVersion = true;

        component['updateApprovalStates'](true, false);

        expect(component.isAPIRevisionApprovalDisabled).toBe(true);
        expect(component.apiRevisionApprovalMessages).toEqual([
          "A package version must be set before approving.",
          "Copilot review must be completed before approving."
        ]);
      });

      it('should show copilot message when version exists but copilot review needed', () => {
        component.activeAPIRevision!.approvers = [];
        component.isMissingPackageVersion = false;

        component['updateApprovalStates'](true, false);

        expect(component.isAPIRevisionApprovalDisabled).toBe(true);
        expect(component.apiRevisionApprovalMessages).toEqual(["Copilot review must be completed before approving."]);
      });

      it('should show combined message when copilot review required and must fix remain', () => {
        component.activeAPIRevision!.approvers = [];
        component.isMissingPackageVersion = false;
        component.qualityScore = { score: 50, unresolvedMustFixCount: 2, unresolvedShouldFixCount: 0, unresolvedSuggestionCount: 0, unresolvedQuestionCount: 0, unresolvedUnknownCount: 0, totalUnresolvedCount: 2 };
        component.unresolvedMustFixCount = 2;

        component['updateApprovalStates'](true, false);

        expect(component.isAPIRevisionApprovalDisabled).toBe(true);
        expect(component.apiRevisionApprovalMessages).toEqual([
          "Copilot review must be completed before approving.",
          "Cannot approve due to outstanding \"Must Fix\" comments."
        ]);
      });

      it('should show must fix message when copilot review is completed but must fix remain', () => {
        component.activeAPIRevision!.approvers = [];
        component.isMissingPackageVersion = false;
        component.qualityScore = { score: 50, unresolvedMustFixCount: 1, unresolvedShouldFixCount: 0, unresolvedSuggestionCount: 0, unresolvedQuestionCount: 0, unresolvedUnknownCount: 0, totalUnresolvedCount: 1 };
        component.unresolvedMustFixCount = 1;

        component['updateApprovalStates'](true, true);

        expect(component.isAPIRevisionApprovalDisabled).toBe(true);
        expect(component.apiRevisionApprovalMessages).toEqual(["Cannot approve due to outstanding \"Must Fix\" comments."]);
      });

      it('should show must fix message when copilot is not supported but must fix remain', () => {
        component.activeAPIRevision!.approvers = [];
        component.isMissingPackageVersion = false;
        component.isCopilotReviewSupported = false;
        component.qualityScore = { score: 50, unresolvedMustFixCount: 1, unresolvedShouldFixCount: 0, unresolvedSuggestionCount: 0, unresolvedQuestionCount: 0, unresolvedUnknownCount: 0, totalUnresolvedCount: 1 };
        component.unresolvedMustFixCount = 1;

        component['updateApprovalStates'](true, false);

        expect(component.isAPIRevisionApprovalDisabled).toBe(true);
        expect(component.apiRevisionApprovalMessages).toEqual(["Cannot approve due to outstanding \"Must Fix\" comments."]);
      });
    });
  });
});
