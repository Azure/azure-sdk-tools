import { Component, EventEmitter, Input, OnChanges, OnInit, Output, SimpleChanges } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { InputSwitchChangeEvent } from 'primeng/inputswitch';
import { getQueryParams } from 'src/app/_helpers/router-helpers';
import { CodeLineRowNavigationDirection, FULL_DIFF_STYLE, getAIReviewNotifiationInfo, mapLanguageAliases, TREE_DIFF_STYLE } from 'src/app/_helpers/common-helpers';
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
import { CodeLineSearchInfo } from 'src/app/_models/codeLineSearchInfo';
import { environment } from 'src/environments/environment';
import { MessageService } from 'primeng/api';
import { ToastMessageData } from 'src/app/_models/toastMessageModel';
import { CommentsService } from 'src/app/_services/comments/comments.service';
import { SignalRService } from 'src/app/_services/signal-r/signal-r.service';
import { AIReviewJobCompletedDto } from 'src/app/_dtos/aiReviewJobCompletedDto';
import { NotificationsService } from 'src/app/_services/notifications/notifications.service';
import { SiteNotification } from 'src/app/_models/notificationsModel';
import { SiteNotificationDto, SiteNotificationStatus } from 'src/app/_dtos/siteNotificationDto';
import { AzureEngSemanticVersion } from 'src/app/_models/azureEngSemanticVersion';

@Component({
  selector: 'app-review-page-options',
  templateUrl: './review-page-options.component.html',
  styleUrls: ['./review-page-options.component.scss']
})
export class ReviewPageOptionsComponent implements OnInit, OnChanges {
  @Input() loadingStatus : 'loading' | 'completed' | 'failed' = 'loading';
  @Input() userProfile: UserProfile | undefined;
  @Input() isDiffView: boolean = false;
  @Input() contentHasDiff: boolean | undefined = false;
  @Input() diffStyleInput: string | undefined;
  @Input() review : Review | undefined = undefined;
  @Input() activeAPIRevision : APIRevision | undefined = undefined;
  @Input() diffAPIRevision : APIRevision | undefined = undefined;
  @Input() preferredApprovers: string[] = [];
  @Input() hasFatalDiagnostics : boolean = false;
  @Input() hasActiveConversation : boolean = false;
  @Input() hasHiddenAPIs : boolean = false;
  @Input() hasHiddenAPIThatIsDiff : boolean = false;
  @Input() codeLineSearchInfo : CodeLineSearchInfo | undefined = undefined;

  @Output() diffStyleEmitter : EventEmitter<string> = new EventEmitter<string>();
  @Output() showCommentsEmitter : EventEmitter<boolean> = new EventEmitter<boolean>();
  @Output() showSystemCommentsEmitter : EventEmitter<boolean> = new EventEmitter<boolean>();
  @Output() showDocumentationEmitter : EventEmitter<boolean> = new EventEmitter<boolean>();
  @Output() showHiddenAPIEmitter : EventEmitter<boolean> = new EventEmitter<boolean>();
  @Output() showLeftNavigationEmitter : EventEmitter<boolean> = new EventEmitter<boolean>();
  @Output() disableCodeLinesLazyLoadingEmitter : EventEmitter<boolean> = new EventEmitter<boolean>();
  @Output() markAsViewedEmitter : EventEmitter<boolean> = new EventEmitter<boolean>();
  @Output() subscribeEmitter : EventEmitter<boolean> = new EventEmitter<boolean>();
  @Output() showLineNumbersEmitter : EventEmitter<boolean> = new EventEmitter<boolean>();
  @Output() apiRevisionApprovalEmitter : EventEmitter<boolean> = new EventEmitter<boolean>();
  @Output() reviewApprovalEmitter : EventEmitter<boolean> = new EventEmitter<boolean>();
  @Output() namespaceApprovalEmitter : EventEmitter<boolean> = new EventEmitter<boolean>();
  @Output() commentThreadNavaigationEmitter : EventEmitter<CodeLineRowNavigationDirection> = new EventEmitter<CodeLineRowNavigationDirection>();
  @Output() diffNavaigationEmitter : EventEmitter<CodeLineRowNavigationDirection> = new EventEmitter<CodeLineRowNavigationDirection>();
  @Output() copyReviewTextEmitter : EventEmitter<boolean> = new EventEmitter<boolean>();
  @Output() codeLineSearchTextEmitter : EventEmitter<string> = new EventEmitter<string>();
  @Output() codeLineSearchInfoEmitter : EventEmitter<CodeLineSearchInfo> = new EventEmitter<CodeLineSearchInfo>();

  private destroy$ = new Subject<void>();

  webAppUrl : string = this.configService.webAppUrl
  assetsPath : string = environment.assetsPath;

  showCommentsSwitch : boolean = true;
  showSystemCommentsSwitch : boolean = true;
  showDocumentationSwitch : boolean = true;
  showHiddenAPISwitch : boolean = false;
  showLeftNavigationSwitch : boolean = true;
  markedAsViewSwitch : boolean = false;
  subscribeSwitch : boolean = false;
  showLineNumbersSwitch : boolean = true;
  disableCodeLinesLazyLoading: boolean = false;
  isCopilotReviewSupported: boolean = true;

  canToggleApproveAPIRevision: boolean = false;
  activeAPIRevisionIsApprovedByCurrentUser: boolean = false;
  isAPIRevisionApprovalDisabled: boolean = false;
  apiRevisionApprovalMessage: string = '';
  apiRevisionApprovalBtnClass: string = '';
  apiRevisionApprovalBtnLabel: string = '';
  showAPIRevisionApprovalModal: boolean = false;
  showDisableCodeLinesLazyLoadingModal: boolean = false;
  overrideActiveConversationforApproval : boolean = false;
  overrideFatalDiagnosticsforApproval : boolean = false;

  canApproveReview: boolean | undefined = undefined;
  reviewIsApproved: boolean | undefined = undefined;
  reviewApprover: string = 'azure-sdk';
  copyReviewTextButtonText : string = 'Copy review text';
  generateAIReviewButtonText : string = 'Generate copilot review';
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

  associatedPullRequests  : PullRequestModel[] = [];
  pullRequestsOfAssociatedAPIRevisions : PullRequestModel[] = [];
  CodeLineRowNavigationDirection = CodeLineRowNavigationDirection;

  //Approvers Options
  selectedApprovers: string[] = [];

  diffStyleOptions : any[] = [
    { label: 'Changed types only', value: TREE_DIFF_STYLE },
    { label: 'Full diff', value: FULL_DIFF_STYLE }
  ];
  selectedDiffStyle : string = this.diffStyleOptions[0];

  changeHistoryIcons : any = {
    'created': 'bi bi-plus-circle-fill created',
    'approved': 'bi bi-check-circle-fill approved',
    'approvalReverted': 'bi bi-arrow-left-circle-fill approval-reverted',
    'deleted': 'bi bi-trash3-fill deleted',
    'unDeleted': 'bi bi-plus-circle-fill undeleted'
  };

  constructor(
    private configService: ConfigService, private reviewsService: ReviewsService, private route: ActivatedRoute,
    private router: Router,  private apiRevisionsService: APIRevisionsService, private commentsService: CommentsService,
    private pullRequestService: PullRequestsService, private messageService: MessageService,
    private signalRService: SignalRService, private notificationsService: NotificationsService) { }

  async ngOnInit() {
    this.activeAPIRevision?.assignedReviewers.map(revision => this.selectedApprovers.push(revision.assingedTo));

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
  }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['diffStyleInput'] && changes['diffStyleInput'].currentValue != undefined) {
      this.setSelectedDiffStyle();
    }

    if (changes['userProfile'] && changes['userProfile'].currentValue != undefined) {
      this.setSubscribeSwitch();
      this.setMarkedAsViewSwitch();
      this.setPageOptionValues();
    }

    if (changes['activeAPIRevision'] && changes['activeAPIRevision'].currentValue != undefined) {
      this.setMarkedAsViewSwitch();
      this.selectedApprovers = this.activeAPIRevision!.assignedReviewers.map(reviewer => reviewer.assingedTo);
      this.isCopilotReviewSupported = this.isCopilotReviewSupportedForPackage();
      this.setAPIRevisionApprovalStates();
      this.setPullRequestsInfo();
      if (this.activeAPIRevision?.copilotReviewInProgress) {
        this.aiReviewGenerationState = 'InProgress';
        this.generateAIReviewButtonText = 'Generating review...';
      } else if (this.activeAPIRevision?.hasAutoGeneratedComments) {
        this.aiReviewGenerationState = 'Completed';
        this.generateAIReviewButtonText = 'Generate copilot review';
      }
    }

    if (changes['diffAPIRevision']) {
      this.setAPIRevisionApprovalStates();
    }

    if (changes['review'] && changes['review'].currentValue != undefined) {
      this.setSubscribeSwitch();
      this.setReviewApprovalStatus();
      this.setNamespaceReviewStates();
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
    this.diffStyleEmitter.emit(event.value.value);
  }

  /**
 * Callback for commentSwitch Change
 * @param event the Filter event
 */
  onCommentsSwitchChange(event: InputSwitchChangeEvent) {
    this.updateRoute();
    this.showCommentsEmitter.emit(event.checked);
  }

   /**
  * Callback for systemCommentSwitch Change
  * @param event the Filter event
  */
  onShowSystemCommentsSwitchChange(event: InputSwitchChangeEvent) {
    this.updateRoute();
    this.showSystemCommentsEmitter.emit(event.checked);
  }

  /**
  * Callback for showDocumentationSwitch Change
  * @param event the Filter event
  */
  onShowDocumentationSwitchChange(event: InputSwitchChangeEvent) {
    this.updateRoute();
    this.showDocumentationEmitter.emit(event.checked);
  }

  /**
  * Callback for showLeftnavigationSwitch Change
  * @param event the Filter event
  */
  onShowLeftNavigationSwitchChange(event: InputSwitchChangeEvent) {
    this.showLeftNavigationEmitter.emit(event.checked);
  }

  /**
   * Disable Lazy Loading
   * @param event the Filter event
   */
  onDisableLazyLoadingSwitchChange(event: InputSwitchChangeEvent) {
    if (event.checked) {
      this.showDisableCodeLinesLazyLoadingModal = true;
    } else {
      this.disableCodeLinesLazyLoadingEmitter.emit(event.checked);
    }
  }

  /**
   * Handle disable lazy loading modal hide
   */
  onDisableLazyLoadingModalHide() {
    this.showDisableCodeLinesLazyLoadingModal = false;
  }

  /**
   * Confirm disable lazy loading
   */
  onDisableLazyLoadingConfirm() {
    this.disableCodeLinesLazyLoadingEmitter.emit(true);
    this.showDisableCodeLinesLazyLoadingModal = false;
  }

  /**
   * Cancel disable lazy loading
   */
  onDisableLazyLoadingCancel() {
    this.showDisableCodeLinesLazyLoadingModal = false;
  }

  /**
  * Callback for markedAsViewSwitch Change
  * @param event the Filter event
  */
  onMarkedAsViewedSwitchChange(event: InputSwitchChangeEvent) {
    this.markAsViewedEmitter.emit(event.checked);
  }

  /**
  * Callback for markedAsViewSwitch Change
  * @param event the Filter event
  */
  onSubscribeSwitchChange(event: InputSwitchChangeEvent) {
    this.subscribeEmitter.emit(event.checked);
  }

  /**
   * Callback for showLineNumbersSwitch Change
   * @param event the Filter event
   */
  onShowLineNumbersSwitchChange(event: InputSwitchChangeEvent) {
    this.showLineNumbersEmitter.emit(event.checked);
  }

   /**
   * Callback for showHiddenAPISwitch Change
   * @param event the Filter event
   */
  onShowHiddenAPISwitchChange(event: InputSwitchChangeEvent) {
    this.showHiddenAPIEmitter.emit(event.checked);
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
    this.selectedDiffStyle = (inputDiffStyle) ? inputDiffStyle : this.diffStyleOptions[0];
  }

  setPageOptionValues() {
    this.showCommentsSwitch = this.userProfile?.preferences.showComments ?? this.showCommentsSwitch;
    this.showSystemCommentsSwitch = this.userProfile?.preferences.showSystemComments ?? this.showSystemCommentsSwitch;
    this.showDocumentationSwitch = this.userProfile?.preferences.showDocumentation ?? this.showDocumentationSwitch;
    this.disableCodeLinesLazyLoading = this.userProfile?.preferences.disableCodeLinesLazyLoading ?? this.disableCodeLinesLazyLoading;
    this.showLineNumbersSwitch = (this.userProfile?.preferences.hideLineNumbers) ? false : this.showLineNumbersSwitch;
    this.showLeftNavigationSwitch = (this.userProfile?.preferences.hideLeftNavigation) ? false : this.showLeftNavigationSwitch;
    this.showHiddenAPISwitch = (this.userProfile?.preferences.showHiddenApis || this.hasHiddenAPIThatIsDiff || this.showHiddenAPISwitch) ? true : false;
  }

  setAPIRevisionApprovalStates() {
    const language = this.review?.language;
    const packageVersion = this.activeAPIRevision?.packageVersion;

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

    this.isAPIRevisionApprovalDisabled = isReviewByCopilotRequired && !isVersionReviewedByCopilot && !this.activeAPIRevisionIsApprovedByCurrentUser;

    if (this.canToggleApproveAPIRevision) {
      if (this.isAPIRevisionApprovalDisabled) {
        this.apiRevisionApprovalBtnClass = "btn btn-outline-secondary disabled";
      } else {
        this.apiRevisionApprovalBtnClass = (this.activeAPIRevisionIsApprovedByCurrentUser) ? "btn btn-outline-secondary" : "btn btn-success";
      }
      this.apiRevisionApprovalBtnLabel = (this.activeAPIRevisionIsApprovedByCurrentUser) ? "Revert API Approval" : "Approve";
      this.apiRevisionApprovalMessage = this.activeAPIRevisionIsApprovedByCurrentUser ? "" :
        this.isAPIRevisionApprovalDisabled ? "To approve the current API revision, it must first be reviewed by Copilot" :
        "Approves the Current API Revision";
    } else {
      this.apiRevisionApprovalBtnClass = "btn btn-outline-secondary";
      this.apiRevisionApprovalBtnLabel = (this.activeAPIRevisionIsApprovedByCurrentUser) ? "Revert API Approval" : "Approve";
    }
  }
  setReviewApprovalStatus() {
    this.reviewIsApproved = !!this.review?.isApproved;
    if (this.reviewIsApproved) {
      this.reviewApprover = this.review?.changeHistory.find(ch => ch.changeAction === 'approved')?.changedBy ?? 'azure-sdk';
    }
  }

  setNamespaceReviewStates() {
    // Only show namespace review request for TypeSpec language AND if feature is enabled
    this.canRequestNamespaceReview = this.review?.language === 'TypeSpec' && this.namespaceReviewEnabled;
    console.log("Namespace review request can be made:",  this.namespaceReviewEnabled);
    // Always keep the button available for requesting namespace review
    this.isNamespaceReviewRequested = false;

    if (this.isNamespaceReviewInProgress && (this.review?.namespaceReviewStatus === 'pending' || this.review?.namespaceReviewStatus === 'approved')) {
      this.isNamespaceReviewInProgress = false;
    }

    // Update button state
    this.updateNamespaceReviewButtonState();
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
          for (const pr of response) {
            if (pr.reviewId != this.activeAPIRevision?.reviewId) {
              this.pullRequestsOfAssociatedAPIRevisions.push(pr);
            }
          }
        }
      });
    }
  }

  setSubscribeSwitch() {
    this.subscribeSwitch = (this.userProfile && this.review) ? this.review!.subscribers.includes(this.userProfile?.email!) : this.subscribeSwitch;
  }

  setMarkedAsViewSwitch() {
    this.markedAsViewSwitch = (this.activeAPIRevision && this.userProfile)? this.activeAPIRevision!.viewedBy.includes(this.userProfile?.userName!): this.markedAsViewSwitch;
  }

  copyReviewText(event: Event) {
    const icon = (event?.target as Element).firstChild as HTMLElement;

    icon.classList.remove('bi-clipboard');
    icon.classList.add('bi-clipboard-check');
    this.copyReviewTextButtonText = 'Review text copied!';

    setTimeout(() => {
      this.copyReviewTextButtonText = 'Copy review text';
      icon.classList.remove('bi-clipboard-check');
      icon.classList.add('bi-clipboard');
    }, 1500);

    this.copyReviewTextEmitter.emit(this.isDiffView);
  }

  generateAIReview() {
    this.aiReviewGenerationState = 'InProgress';
    this.generateAIReviewButtonText = 'Generating review...';
    const diffApiRevisionId = this.diffAPIRevision ? this.diffAPIRevision.id : undefined;

    this.apiRevisionsService.generateAIReview(this.activeAPIRevision!.reviewId, this.activeAPIRevision!.id, diffApiRevisionId).pipe(take(1)).subscribe({
      error: (error: any) => {
        this.aiReviewGenerationState = 'Failed';
        this.generateAIReviewButtonText = 'Failed to generate copilot review';
        const message = 'Failed to generate copilot review';
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
    this.commentsService.clearAutoGeneratedComments(this.activeAPIRevision?.id!).pipe(take(1)).subscribe({
      next: (response) => {
        const messgaeData : ToastMessageData = {
          action: 'RefreshPage',
        };
        this.messageService.add({ severity: 'success', icon: 'bi bi-check-circle', summary: 'Comments Cleared', detail: 'All auto-generated comments for this APIRevision has been deleted.', data: messgaeData, key: 'bc', life: 60000 });
      },
      error: (error) => {
        this.messageService.add({ severity: 'error', icon: 'bi bi-exclamation-triangle', summary: 'Comment Error', detail: 'Failed to clear auto-generated comments.', key: 'bc', life: 3000 });
      }
    });
  }

  navigateCommentThread(direction: CodeLineRowNavigationDirection) {
    this.commentThreadNavaigationEmitter.emit(direction);
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
            this.generateAIReviewButtonText = 'Failed to generate copilot review';
          }
          const notificationInfo = getAIReviewNotifiationInfo(aiReviewUpdate, window.location.origin);
          if (notificationInfo) {
            if (aiReviewUpdate.apirevisionId === this.activeAPIRevision?.id) {
              this.messageService.add(notificationInfo[1]);
            }
          }

          setTimeout(() => {
            this.aiReviewGenerationState = 'Completed';
            this.generateAIReviewButtonText = 'Generate copilot review';
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
    if (!this.isCopilotReviewSupported) return false;
    if (this.isPreviewVersion()) return false;
    if (this.activeAPIRevisionIsApprovedByCurrentUser) return false;

    return isReviewByCopilotRequired && !isVersionReviewedByCopilot;
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
