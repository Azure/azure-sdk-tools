import { Component, EventEmitter, Input, OnChanges, OnInit, OnDestroy, Output, SimpleChanges } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ToggleSwitchChangeEvent } from 'primeng/toggleswitch';
import { getQueryParams } from 'src/app/_helpers/router-helpers';
import { CodeLineRowNavigationDirection, FULL_DIFF_STYLE, getAIReviewNotificationInfo, mapLanguageAliases, TREE_DIFF_STYLE } from 'src/app/_helpers/common-helpers';
import { Review } from 'src/app/_models/review';
import { APIRevision } from 'src/app/_models/revision';
import { ConfigService } from 'src/app/_services/config/config.service';
import { ReviewsService } from 'src/app/_services/reviews/reviews.service';
import { APIRevisionsService } from 'src/app/_services/revisions/revisions.service';
import { debounceTime, distinctUntilChanged, Subject, take, takeUntil, combineLatest, of } from 'rxjs';
import { UserProfile } from 'src/app/_models/userProfile';
import { PullRequestsService } from 'src/app/_services/pull-requests/pull-requests.service';
import { PullRequestModel } from 'src/app/_models/pullRequestModel';
import { FormControl } from '@angular/forms';
import { PermissionsService } from 'src/app/_services/permissions/permissions.service';
import { ReviewContextService } from 'src/app/_services/review-context/review-context.service';
import { CodeLineSearchInfo } from 'src/app/_models/codeLineSearchInfo';
import { environment } from 'src/environments/environment';
import { MessageService, ConfirmationService } from 'primeng/api';
import { CommentsService } from 'src/app/_services/comments/comments.service';
import { SignalRService } from 'src/app/_services/signal-r/signal-r.service';
import { AIReviewJobCompletedDto } from 'src/app/_dtos/aiReviewJobCompletedDto';
import { NotificationsService } from 'src/app/_services/notifications/notifications.service';
import { SiteNotification } from 'src/app/_models/notificationsModel';
import { SiteNotificationDto, SiteNotificationStatus } from 'src/app/_dtos/siteNotificationDto';
import { AzureEngSemanticVersion } from 'src/app/_models/azureEngSemanticVersion';
import { ReviewQualityScore } from 'src/app/_models/reviewQualityScore';

// Constants for AI review button text
const AI_REVIEW_BUTTON_TEXT = {
  GENERATE: 'Request Copilot review',
  GENERATING: 'Generating...',
  FAILED: 'Failed to generate Copilot review'
} as const;

@Component({
    selector: 'app-review-page-options',
    templateUrl: './review-page-options.component.html',
    styleUrls: ['./review-page-options.component.scss'],
    standalone: false
})
export class ReviewPageOptionsComponent implements OnInit, OnChanges, OnDestroy {
  @Input() loadingStatus : 'loading' | 'completed' | 'failed' = 'loading';
  @Input() userProfile: UserProfile | undefined;
  @Input() isDiffView: boolean = false;
  @Input() contentHasDiff: boolean | undefined = false;
  @Input() diffStyleInput: string | undefined;
  @Input() review : Review | undefined = undefined;
  @Input() activeAPIRevision : APIRevision | undefined = undefined;
  @Input() diffAPIRevision : APIRevision | undefined = undefined;
  @Input() hasFatalDiagnostics : boolean = false;
  @Input() hasActiveConversation : boolean = false;
  @Input() hasHiddenAPIs : boolean = false;
  @Input() hasHiddenAPIThatIsDiff : boolean = false;
  @Input() codeLineSearchInfo : CodeLineSearchInfo | undefined = undefined;

  @Output() diffStyleEmitter : EventEmitter<string> = new EventEmitter<string>();
  @Output() showCommentsEmitter : EventEmitter<boolean> = new EventEmitter<boolean>();
  @Output() showSystemCommentsEmitter : EventEmitter<boolean> = new EventEmitter<boolean>();
  @Output() showDocumentationEmitter : EventEmitter<boolean> = new EventEmitter<boolean>();
  @Output() subscribeEmitter : EventEmitter<boolean> = new EventEmitter<boolean>();
  @Output() showLineNumbersEmitter : EventEmitter<boolean> = new EventEmitter<boolean>();
  @Output() apiRevisionApprovalEmitter : EventEmitter<boolean> = new EventEmitter<boolean>();
  @Output() reviewApprovalEmitter : EventEmitter<boolean> = new EventEmitter<boolean>();
  @Output() namespaceApprovalEmitter : EventEmitter<boolean> = new EventEmitter<boolean>();
  @Output() commentThreadNavigationEmitter : EventEmitter<CodeLineRowNavigationDirection> = new EventEmitter<CodeLineRowNavigationDirection>();
  @Output() diffNavigationEmitter : EventEmitter<CodeLineRowNavigationDirection> = new EventEmitter<CodeLineRowNavigationDirection>();
  @Output() codeLineSearchTextEmitter : EventEmitter<string> = new EventEmitter<string>();
  @Output() codeLineSearchInfoEmitter : EventEmitter<CodeLineSearchInfo> = new EventEmitter<CodeLineSearchInfo>();

  private destroy$ = new Subject<void>();

  webAppUrl : string = this.configService.webAppUrl
  assetsPath : string = environment.assetsPath;

  showCommentsSwitch : boolean = true;
  showSystemCommentsSwitch : boolean = true;
  showDocumentationSwitch : boolean = true;
  subscribeSwitch : boolean = false;
  showLineNumbersSwitch : boolean = true;
  isCopilotReviewSupported: boolean = true;
  isAdmin: boolean = false;

  canToggleApproveAPIRevision: boolean = false;
  activeAPIRevisionIsApprovedByCurrentUser: boolean = false;
  isAPIRevisionApprovalDisabled: boolean = false;
  isMissingPackageVersion: boolean = false;
  apiRevisionApprovalMessage: string = '';
  apiRevisionApprovalBtnClass: string = '';
  apiRevisionApprovalBtnLabel: string = '';
  showAPIRevisionApprovalModal: boolean = false;
  overrideActiveConversationforApproval : boolean = false;
  overrideFatalDiagnosticsforApproval : boolean = false;

  get canApproveForReviewLanguage(): boolean {
    if (!this.userProfile?.permissions || !this.review?.language) {
      return false;
    }
    return this.permissionsService.isApproverFor(this.userProfile.permissions, this.review.language);
  }

  canApproveReview: boolean | undefined = undefined;
  reviewIsApproved: boolean | undefined = undefined;
  reviewApprover: string = 'azure-sdk';
  generateAIReviewButtonText : string = AI_REVIEW_BUTTON_TEXT.GENERATE;
  readonly AI_REVIEW_BUTTON_TEXT = AI_REVIEW_BUTTON_TEXT;
  aiReviewGenerationState : 'NotStarted' | 'InProgress' | 'Completed' | 'Failed' = 'NotStarted';

  // Namespace review properties
  canRequestNamespaceReview: boolean = false;
  isNamespaceReviewRequested: boolean = false;
  isNamespaceReviewInProgress: boolean = false;
  namespaceReviewBtnClass: string = '';
  namespaceReviewBtnLabel: string = '';
  namespaceReviewMessage: string = '';
  namespaceReviewEnabled: boolean = false; // Feature flag from Azure App Configuration

  codeLineSearchText: FormControl = new FormControl('');

  qualityScore: ReviewQualityScore | undefined = undefined;
  qualityScoreLoading: boolean = false;

  associatedPullRequests  : PullRequestModel[] = [];
  pullRequestsOfAssociatedAPIRevisions : PullRequestModel[] = [];
  CodeLineRowNavigationDirection = CodeLineRowNavigationDirection;

  //Approvers Options
  selectedApprovers: string[] = [];
  filteredApprovers: string[] = [];
  reviewerSearchText: string = '';
  private languageApprovers: string[] = [];

  diffStyleOptions : any[] = [
    { label: 'Changed types only', value: TREE_DIFF_STYLE },
    { label: 'Full diff', value: FULL_DIFF_STYLE }
  ];
  selectedDiffStyle : string = this.diffStyleOptions[0].value;

  constructor(
    private configService: ConfigService, private reviewsService: ReviewsService, private route: ActivatedRoute,
    private router: Router,  private apiRevisionsService: APIRevisionsService, private commentsService: CommentsService,
    private pullRequestService: PullRequestsService, private messageService: MessageService,
    private signalRService: SignalRService, private notificationsService: NotificationsService,
    private permissionsService: PermissionsService, private reviewContextService: ReviewContextService, private confirmationService: ConfirmationService) { }

  async ngOnInit() {
    this.activeAPIRevision?.assignedReviewers.map(revision => this.selectedApprovers.push(revision.assingedTo));

    // Subscribe to language approvers from context service
    this.reviewContextService.getLanguageApprovers$().pipe(takeUntil(this.destroy$)).subscribe(approvers => {
      this.languageApprovers = approvers;
      this.filteredApprovers = this.sortSelectedFirst([...approvers]);
      this.reviewerSearchText = '';
    });

    // Load EnableNamespaceReview feature flag from Azure App Configuration
    this.reviewsService.getEnableNamespaceReview().pipe(take(1)).subscribe({
      next: (enabled: any) => {
        // Handle null/undefined values from the API and convert string "true"/"false" to boolean
        this.namespaceReviewEnabled = enabled === true || enabled === 'true';
        this.setNamespaceReviewStates();
      },
      error: (error: any) => {
        this.namespaceReviewEnabled = false; // Default to false on error
        this.setNamespaceReviewStates();
      }
    });

    this.codeLineSearchText.valueChanges.pipe(
      debounceTime(500),
      distinctUntilChanged(),
      takeUntil(this.destroy$)
    ).subscribe((searchText: string) => {
      this.codeLineSearchTextEmitter.emit(searchText);
    });
    this.handleRealTimeAIReviewUpdates();
    this.handleSiteNotification();

    this.commentsService.qualityScoreRefreshNeeded$.pipe(takeUntil(this.destroy$)).subscribe(() => {
      this.fetchQualityScore();
    });
  }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['diffStyleInput'] && changes['diffStyleInput'].currentValue != undefined) {
      this.setSelectedDiffStyle();
    }

    if (changes['userProfile'] && changes['userProfile'].currentValue != undefined) {
      this.setSubscribeSwitch();
      this.setPageOptionValues();
      this.isAdmin = this.permissionsService.isAdmin(this.userProfile?.permissions);
    }

    if (changes['activeAPIRevision'] && changes['activeAPIRevision'].currentValue != undefined) {
      this.selectedApprovers = this.activeAPIRevision!.assignedReviewers.map(reviewer => reviewer.assingedTo);
      this.isCopilotReviewSupported = this.isCopilotReviewSupportedForPackage();
      this.setAPIRevisionApprovalStates();
      this.setPullRequestsInfo();
      this.setNamespaceReviewStates();
      this.fetchQualityScore();
      if (this.activeAPIRevision?.copilotReviewInProgress) {
        this.aiReviewGenerationState = 'InProgress';
        this.generateAIReviewButtonText = AI_REVIEW_BUTTON_TEXT.GENERATING;
      } else if (this.activeAPIRevision?.hasAutoGeneratedComments) {
        this.aiReviewGenerationState = 'Completed';
        this.generateAIReviewButtonText = AI_REVIEW_BUTTON_TEXT.GENERATE;
      }
    }

    if (changes['diffAPIRevision']) {
      this.setAPIRevisionApprovalStates();
    }

    if (changes['review'] && changes['review'].currentValue != undefined) {
      this.setSubscribeSwitch();
      this.setReviewApprovalStatus();
      this.updateDiffStyle();

      // Reset loading state when review data is updated (indicating the request completed)
      if (this.isNamespaceReviewInProgress) {
        // Reset loading if status changed to pending or approved (indicating request completed)
        if (changes['review'].currentValue.namespaceReviewStatus === 'pending' ||
            changes['review'].currentValue.namespaceReviewStatus === 'approved') {
          this.isNamespaceReviewInProgress = false;
          this.updateNamespaceReviewButtonState();
        }
      }
    }

    if (changes['hasHiddenAPIThatIsDiff']) {
      this.setPageOptionValues();
    }
  }

  /**
 * Callback to on onlyDiff Change
 * @param event the Filter event
 */
  onDiffStyleChange(event: any) {
    this.updateRoute();
    this.diffStyleEmitter.emit(event.value);
  }

  /**
 * Callback for commentSwitch Change
 * @param event the Filter event
 */
  onCommentsSwitchChange(event: ToggleSwitchChangeEvent) {
    this.updateRoute();
    this.showCommentsEmitter.emit(event.checked);
  }

   /**
  * Callback for systemCommentSwitch Change
  * @param event the Filter event
  */
  onShowSystemCommentsSwitchChange(event: ToggleSwitchChangeEvent) {
    this.updateRoute();
    this.showSystemCommentsEmitter.emit(event.checked);
  }

  /**
  * Callback for showDocumentationSwitch Change
  * @param event the Filter event
  */
  onShowDocumentationSwitchChange(event: ToggleSwitchChangeEvent) {
    this.updateRoute();
    this.showDocumentationEmitter.emit(event.checked);
  }

  /**
  * Callback for showLeftnavigationSwitch Change
  * @param event the Filter event
  */
  onSubscribeSwitchChange(event: ToggleSwitchChangeEvent) {
    this.subscribeEmitter.emit(event.checked);
  }

  /**
   * Callback for Subscribe button click
   */
  onSubscribeButtonClick() {
    this.subscribeSwitch = !this.subscribeSwitch;
    this.subscribeEmitter.emit(this.subscribeSwitch);
  }

  /**
   * Callback for showLineNumbersSwitch Change
   * @param event the Filter event
   */
  onShowLineNumbersSwitchChange(event: ToggleSwitchChangeEvent) {
    this.showLineNumbersEmitter.emit(event.checked);
  }

  handleAssignedReviewersChange() {
    const existingApprovers = new Set(this.activeAPIRevision!.assignedReviewers.map(reviewer => reviewer.assingedTo));
    const currentApprovers = new Set(this.selectedApprovers);
    const isSelectedApproversChanged = existingApprovers.size !== currentApprovers.size ||
                      [...existingApprovers].some(approver => !currentApprovers.has(approver));

    if (isSelectedApproversChanged) {
      this.apiRevisionsService.updateSelectedReviewers(this.activeAPIRevision!.reviewId, this.activeAPIRevision!.id, currentApprovers).pipe(take(1)).subscribe({
        next: (response: APIRevision) => {
          this.activeAPIRevision = response;
          }
      });
    }
  }

  filterReviewers() {
    if (!this.reviewerSearchText) {
      this.filteredApprovers = this.sortSelectedFirst([...this.languageApprovers]);
    } else {
      const searchLower = this.reviewerSearchText.toLowerCase();
      this.filteredApprovers = this.sortSelectedFirst(
        this.languageApprovers.filter(approver =>
          approver.toLowerCase().includes(searchLower)
        ),
        searchLower
      );
    }
  }

  resetReviewerSearch() {
    this.reviewerSearchText = '';
    this.filteredApprovers = this.sortSelectedFirst([...this.languageApprovers]);
  }

  /**
   * Merges selected approvers (who may not be language approvers) into the list,
   * then sorts so selected reviewers appear first — matching GitHub's reviewer dropdown behavior.
   */
  private sortSelectedFirst(approvers: string[], searchFilter?: string): string[] {
    // Ensure currently assigned reviewers always appear, even if not in languageApprovers
    const merged = [...approvers];
    for (const selected of this.selectedApprovers) {
      if (!merged.includes(selected)) {
        // When searching, only add selected reviewers that match the filter
        if (!searchFilter || selected.toLowerCase().includes(searchFilter)) {
          merged.push(selected);
        }
      }
    }
    return merged.sort((a, b) => {
      const aSelected = this.selectedApprovers.includes(a);
      const bSelected = this.selectedApprovers.includes(b);
      if (aSelected && !bSelected) return -1;
      if (!aSelected && bSelected) return 1;
      return 0;
    });
  }

  toggleReviewer(approver: string) {
    const index = this.selectedApprovers.indexOf(approver);
    if (index === -1) {
      this.selectedApprovers = [...this.selectedApprovers, approver];
    } else {
      this.selectedApprovers = this.selectedApprovers.filter(a => a !== approver);
    }
    this.handleAssignedReviewersChange();
  }

  formatSelectedApprovers(approvers: string[]): string {
    return approvers.join(', ');
  }

  updateDiffStyle() {
    if (this.review?.language === 'TypeSpec') {
      this.diffStyleOptions = [
        { label: 'Full Diff', value: FULL_DIFF_STYLE },
      ]
      this.selectedDiffStyle = this.diffStyleOptions[0];
    }
  }

  setSelectedDiffStyle() {
    const inputDiffStyle = this.diffStyleOptions.find(option => option.value === this.diffStyleInput);
    this.selectedDiffStyle = (inputDiffStyle) ? inputDiffStyle.value : this.diffStyleOptions[0].value;
  }

  setPageOptionValues() {
    this.showCommentsSwitch = this.userProfile?.preferences.showComments ?? this.showCommentsSwitch;
    this.showSystemCommentsSwitch = this.userProfile?.preferences.showSystemComments ?? this.showSystemCommentsSwitch;
    this.showDocumentationSwitch = this.userProfile?.preferences.showDocumentation ?? this.showDocumentationSwitch;
    this.showLineNumbersSwitch = (this.userProfile?.preferences.hideLineNumbers) ? false : this.showLineNumbersSwitch;
  }

  setAPIRevisionApprovalStates() {
    const language = this.review?.language;
    const packageVersion = this.activeAPIRevision?.packageVersion;
    this.isMissingPackageVersion = !packageVersion || packageVersion.trim() === '';

    if (language) {
      const isRequired$ = this.reviewsService.getIsReviewByCopilotRequired(language);
      const isVersionReviewed$ = this.review?.id && packageVersion
        ? this.reviewsService.getIsReviewVersionReviewedByCopilot(this.review.id, packageVersion)
        : of(false);

      combineLatest([isRequired$, isVersionReviewed$]).pipe(take(1)).subscribe({
        next: ([isRequired, isVersionReviewed]: [boolean, boolean]) => {
          this.updateApprovalStates(isRequired, isVersionReviewed);
        },
        error: (error) => {
          this.updateApprovalStates(false, false);
        }
      });
    } else {
      this.updateApprovalStates(false, false);
    }
  }

  private updateApprovalStates(isReviewByCopilotRequired: boolean, isVersionReviewedByCopilot: boolean) {
    this.activeAPIRevisionIsApprovedByCurrentUser = this.activeAPIRevision?.approvers.includes(this.userProfile?.userName!)!;
    this.canToggleApproveAPIRevision = (!this.diffAPIRevision || this.diffAPIRevision.approvers.length > 0);

    this.isAPIRevisionApprovalDisabled = this.shouldDisableApproval(isReviewByCopilotRequired, isVersionReviewedByCopilot);

    if (this.canToggleApproveAPIRevision) {
      if (this.isAPIRevisionApprovalDisabled) {
        this.apiRevisionApprovalBtnClass = "btn btn-outline-secondary disabled";
      } else {
        this.apiRevisionApprovalBtnClass = (this.activeAPIRevisionIsApprovedByCurrentUser) ? "btn btn-outline-secondary" : "btn btn-success";
      }
      this.apiRevisionApprovalBtnLabel = (this.activeAPIRevisionIsApprovedByCurrentUser) ? "Revert API Approval" : "Approve";
      this.apiRevisionApprovalMessage = this.activeAPIRevisionIsApprovedByCurrentUser ? "" :
        this.isAPIRevisionApprovalDisabled ? this.getApprovalDisabledMessage(isReviewByCopilotRequired, isVersionReviewedByCopilot) :
        "Approves the Current API Revision";
    } else {
      this.apiRevisionApprovalBtnClass = "btn btn-outline-secondary";
      this.apiRevisionApprovalBtnLabel = (this.activeAPIRevisionIsApprovedByCurrentUser) ? "Revert API Approval" : "Approve";
    }
  }

  private getApprovalDisabledMessage(isReviewByCopilotRequired: boolean, isVersionReviewedByCopilot: boolean): string {
    if (this.isMissingPackageVersion) {
      return "This API revision cannot be approved because it is missing a package version. Please ensure the package version is set.";
    }
    if (this.hasUnresolvedMustFix()) {
      return "Cannot approve while unresolved Must Fix comments remain.";
    }
    if (isReviewByCopilotRequired && !isVersionReviewedByCopilot) {
      return "To approve the current API revision, it must first be reviewed by Copilot";
    }
    return "";
  }
  setReviewApprovalStatus() {
    this.reviewIsApproved = !!this.review?.isApproved;
    if (this.reviewIsApproved) {
      this.reviewApprover = this.review?.changeHistory.find(ch => ch.changeAction === 'approved')?.changedBy ?? 'azure-sdk';
    }
  }

  setNamespaceReviewStates() {
    // Show namespace review section for TypeSpec language when feature is enabled
    // and there are associated API revisions (SDK language reviews)
    // Display different states: approved, requested, or available to request
    this.canRequestNamespaceReview = this.review?.language === 'TypeSpec' &&
                                      this.namespaceReviewEnabled &&
                                      this.pullRequestsOfAssociatedAPIRevisions.length > 0;
    // Always keep the button available for requesting namespace review
    this.isNamespaceReviewRequested = false;

    if (this.isNamespaceReviewInProgress && (this.review?.namespaceReviewStatus === 'pending' || this.review?.namespaceReviewStatus === 'approved')) {
      this.isNamespaceReviewInProgress = false;
    }

    // Update button state
    this.updateNamespaceReviewButtonState();
  }

  fetchQualityScore() {
    if (!this.activeAPIRevision?.id) return;
    this.qualityScoreLoading = true;
    this.apiRevisionsService.getQualityScore(this.activeAPIRevision.id).pipe(take(1)).subscribe({
      next: (score: ReviewQualityScore) => {
        this.qualityScore = score;
        this.qualityScoreLoading = false;
        // Re-evaluate approval states since must-fix count may have changed
        this.setAPIRevisionApprovalStates();
      },
      error: () => {
        this.qualityScore = undefined;
        this.qualityScoreLoading = false;
      }
    });
  }

  getScoreColorClass(): string {
    if (!this.qualityScore) return '';
    if (this.qualityScore.score >= 80) return 'text-success';
    if (this.qualityScore.score >= 50) return 'text-warning';
    return 'text-danger';
  }

  setPullRequestsInfo() {
    if (this.activeAPIRevision?.apiRevisionType === 'pullRequest') {
      this.pullRequestService.getAssociatedPullRequests(this.activeAPIRevision.reviewId, this.activeAPIRevision.id).pipe(take(1)).subscribe({
        next: (response: PullRequestModel[]) => {
          this.associatedPullRequests = response;
        }
      });

      this.pullRequestService.getPullRequestsOfAssociatedAPIRevisions(this.activeAPIRevision.reviewId, this.activeAPIRevision.id).pipe(take(1)).subscribe({
        next: (response: PullRequestModel[]) => {
          // Clear the array first to avoid duplicates
          this.pullRequestsOfAssociatedAPIRevisions = [];
          for (const pr of response) {
            if (pr.reviewId != this.activeAPIRevision?.reviewId) {
              this.pullRequestsOfAssociatedAPIRevisions.push(pr);
            }
          }
          // Re-evaluate namespace review states after associated reviews are loaded
          this.setNamespaceReviewStates();
        }
      });
    }
  }

  setSubscribeSwitch() {
    this.subscribeSwitch = (this.userProfile && this.review) ? this.review!.subscribers.includes(this.userProfile?.email!) : this.subscribeSwitch;
  }

  generateAIReview() {
    this.aiReviewGenerationState = 'InProgress';
    this.generateAIReviewButtonText = AI_REVIEW_BUTTON_TEXT.GENERATING;
    const diffApiRevisionId = this.diffAPIRevision ? this.diffAPIRevision.id : undefined;

    this.apiRevisionsService.generateAIReview(this.activeAPIRevision!.reviewId, this.activeAPIRevision!.id, diffApiRevisionId).pipe(take(1)).subscribe({
      error: (error: any) => {
        this.aiReviewGenerationState = 'Failed';
        this.generateAIReviewButtonText = AI_REVIEW_BUTTON_TEXT.FAILED;
        const message = AI_REVIEW_BUTTON_TEXT.FAILED;
        const severity = 'error';
        const summary = 'AI Comments';
        this.messageService.add({ severity: 'error', icon: 'bi bi-exclamation-triangle', summary: 'AI Comments', detail: message, key: 'bc', life: 5000, closable: true });

        const notification = new SiteNotification(
          this.review?.id,
          this.activeAPIRevision?.id,
          summary,
          message,
          severity
        );
        this.notificationsService.addNotification(notification);
      }
    });
  }

  clearReviewSearch() {
    this.codeLineSearchText.setValue('');
  }

  clearAutoGeneratedComments() {
    this.confirmationService.confirm({
      message: 'Are you sure you want to clear all Copilot-generated comments? This action cannot be undone.',
      header: 'Clear Copilot Comments',
      icon: 'pi pi-exclamation-triangle',
      acceptButtonStyleClass: 'p-button-danger',
      rejectButtonStyleClass: 'p-button-text',
      accept: () => {
        this.commentsService.clearAutoGeneratedComments(this.activeAPIRevision?.id!).pipe(take(1)).subscribe({
          next: () => {
            this.messageService.add({ severity: 'success', summary: 'Comments Cleared', detail: 'All Copilot-generated comments have been cleared.', key: 'bc', life: 3000 });
          },
          error: (error) => {
            this.messageService.add({ severity: 'error', icon: 'bi bi-exclamation-triangle', summary: 'Comment Error', detail: 'Failed to clear auto-generated comments.', key: 'bc', life: 3000 });
          }
        });
      }
    });
  }

  navigateCommentThread(direction: CodeLineRowNavigationDirection) {
    this.commentThreadNavigationEmitter.emit(direction);
  }

  /**
   * Use positive number to navigate to the next search result and negative number to navigate to the previous search result
   * @param number
   */
  navigateSearch(number: 1 | -1) {
    if (number == 1) {
      if (!this.codeLineSearchInfo?.currentMatch?.isTail()) {
        this.codeLineSearchInfo!.currentMatch = this.codeLineSearchInfo?.currentMatch?.next;
        this.codeLineSearchInfoEmitter.emit(this.codeLineSearchInfo!);
      }
    }
    else {
      if (!this.codeLineSearchInfo?.currentMatch?.isHead()) {
        this.codeLineSearchInfo!.currentMatch = this.codeLineSearchInfo?.currentMatch?.prev;
        this.codeLineSearchInfoEmitter.emit(this.codeLineSearchInfo!);
      }
    }
  }

  handleAPIRevisionApprovalAction() {
    if (this.isAPIRevisionApprovalDisabled) {
      return;
    }

    if (!this.activeAPIRevisionIsApprovedByCurrentUser && (this.hasActiveConversation || this.hasFatalDiagnostics)) {
      this.showAPIRevisionApprovalModal = true;
    } else {
      this.toggleAPIRevisionApproval();
    }
  }

  handleReviewApprovalAction() {
    this.reviewApprovalEmitter.emit(true);
  }

  handleSiteNotification(){
    this.signalRService.onNotificationUpdates().pipe(takeUntil(this.destroy$)).subscribe({
      next: (siteNotification: SiteNotificationDto) => {
        if (siteNotification.status === SiteNotificationStatus.Error) {
          const summary = siteNotification.title;
          this.messageService.add({ severity: siteNotification.status, icon: 'bi bi-exclamation-triangle', summary: summary, detail: siteNotification.message, key: 'bc', life: 5000, closable: true });
          const notification = new SiteNotification(
                this.review?.id,
                this.activeAPIRevision?.id,
                siteNotification.summary,
                siteNotification.message,
                siteNotification.status
              );
          this.notificationsService.addNotification(notification);
        }
      }
    });
  }

  handleRealTimeAIReviewUpdates() {
    this.signalRService.onAIReviewUpdates().pipe(takeUntil(this.destroy$)).subscribe({
      next: (aiReviewUpdate: AIReviewJobCompletedDto) => {
        if (aiReviewUpdate.reviewId === this.review?.id && aiReviewUpdate.apirevisionId === this.activeAPIRevision?.id) {
          if (aiReviewUpdate.status === 'Success') {
            this.aiReviewGenerationState = 'Completed';
            this.generateAIReviewButtonText = `Generated ${aiReviewUpdate.noOfGeneratedComments} comments!`;
          } else if (aiReviewUpdate.status === 'Error') {
            this.aiReviewGenerationState = 'Failed';
            this.generateAIReviewButtonText = AI_REVIEW_BUTTON_TEXT.FAILED;
          }
          const notificationInfo = getAIReviewNotificationInfo(aiReviewUpdate, window.location.origin);
          if (notificationInfo) {
            if (aiReviewUpdate.apirevisionId === this.activeAPIRevision?.id) {
              this.messageService.add(notificationInfo[1]);
            }
          }

          setTimeout(() => {
            this.aiReviewGenerationState = 'Completed';
            this.generateAIReviewButtonText = AI_REVIEW_BUTTON_TEXT.GENERATE;
          }, 3000);

        }
      }
    });
  }
  toggleAPIRevisionApproval() {
    this.apiRevisionApprovalEmitter.emit(true);
    this.showAPIRevisionApprovalModal = false;
  }
  handleNamespaceReviewAction() {
    // Only allow if not already requested
    if (!this.isNamespaceReviewRequested && !this.isNamespaceReviewInProgress) {
      // Optimistic UI update - immediately show as in progress
      this.isNamespaceReviewInProgress = true;
      this.updateNamespaceReviewButtonState();

      // Emit the action to parent component
      this.namespaceApprovalEmitter.emit(true);
    }
  }

  updateNamespaceReviewButtonState() {
    if (this.isNamespaceReviewInProgress) {
      this.namespaceReviewBtnClass = "btn btn-outline-primary";
      this.namespaceReviewBtnLabel = "Requesting...";
      this.namespaceReviewMessage = "";
    } else if (this.isNamespaceApproved()) {
      this.namespaceReviewBtnClass = "btn btn-outline-success";
      this.namespaceReviewBtnLabel = "Namespace Review Approved";
      this.namespaceReviewMessage = "";
    } else if (this.isNamespaceReviewRequested) {
      this.namespaceReviewBtnClass = "btn btn-secondary disabled";
      this.namespaceReviewBtnLabel = "Namespace Review Requested";
      this.namespaceReviewMessage = "Please check the review status in associated API Revisions";
    } else {
      this.namespaceReviewBtnClass = "btn btn-success";
      this.namespaceReviewBtnLabel = "Request Namespace Review";
      this.namespaceReviewMessage = "Request namespace reviews for associated API revisions; corresponding reviewers will be notified.";
    }
  }

  /**
   * Reset the namespace review loading state (called when request fails)
   */
  resetNamespaceReviewLoadingState() {
    this.isNamespaceReviewInProgress = false;
    this.updateNamespaceReviewButtonState();
  }

  isNamespaceApproved(): boolean {
    return this.review?.namespaceReviewStatus === 'approved' || false;
  }

  getPullRequestsOfAssociatedAPIRevisionsUrl(pr: PullRequestModel) {
    return `${window.location.origin}/review/${pr.reviewId}?activeApiRevisionId=${pr.apiRevisionId}`;
  }

   /**
   * This updates the page route without triggering a state update (i.e the code lines are not rebuilt, only the URI is updated)
   * This is specifically to remove the nId query parameter from the URI
   */
  updateRoute() {
    let newQueryParams = getQueryParams(this.route); // this automatically excludes the nId query parameter
    this.router.navigate([], { queryParams: newQueryParams, state: { skipStateUpdate: true } });
  }

  deleteReview() {
    if (!this.review?.id) {
      return;
    }

    // Get the revision count and show confirmation dialog
    this.reviewsService.getReviewRevisionCount(this.review.id).pipe(take(1)).subscribe({
      next: (revisionCount: number) => {
        const message = `Are you sure you want to delete this review? It has ${revisionCount} revision(s) that will also be deleted. This action cannot be undone.`;

        this.confirmationService.confirm({
          message: message,
          header: 'Delete Review',
          icon: 'pi pi-exclamation-triangle',
          acceptButtonStyleClass: 'p-button-danger',
          rejectButtonStyleClass: 'p-button-text',
          accept: () => {
            this.reviewsService.deleteReview(this.review!.id).pipe(take(1)).subscribe({
              next: () => {
                this.messageService.add({
                  severity: 'success',
                  summary: 'Review Deleted',
                  detail: 'The review has been successfully deleted.',
                  life: 3000
                });
                // Navigate to reviews list after successful deletion
                this.router.navigate(['/']);
              },
              error: (error) => {
                const errorMessage = error.error || 'Failed to delete the review. Please try again.';
                this.messageService.add({
                  severity: 'error',
                  summary: 'Delete Failed',
                  detail: errorMessage,
                  life: 5000
                });
              }
            });
          }
        });
      },
      error: (error) => {
        this.messageService.add({
          severity: 'error',
          summary: 'Error',
          detail: 'Failed to get revision count. Please try again.',
          life: 3000
        });
      }
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private isPreviewVersion(): boolean {
    if (!this.activeAPIRevision) return false;

    try {
      return new AzureEngSemanticVersion(
        this.activeAPIRevision.packageVersion,
        this.activeAPIRevision.language
      ).isPrerelease;
    } catch {
      return false;
    }
  }

  private shouldDisableApproval(isReviewByCopilotRequired: boolean, isVersionReviewedByCopilot: boolean): boolean {
    if (this.isMissingPackageVersion) return true;
    if(this.activeAPIRevision?.isApproved) return false;
    if (this.hasUnresolvedMustFix()) return true;
    if (!this.isCopilotReviewSupported) return false;
    if (this.isPreviewVersion()) return false;

    return isReviewByCopilotRequired && !isVersionReviewedByCopilot;
  }

  private hasUnresolvedMustFix(): boolean {
    return (this.qualityScore?.unresolvedMustFixCount ?? 0) > 0;
  }

  private isCopilotReviewSupportedForPackage(): boolean {
    if (!this.review?.packageName || !this.review?.language) {
      return true;
    }

    const isAzureRestPackage = this.review.packageName.startsWith("@azure-rest");
    const isJavaScript = this.review.language == "JavaScript";

    const isTypeSpec = this.review.language === "TypeSpec";

    return !(isAzureRestPackage && isJavaScript) && !isTypeSpec;
  }
}
