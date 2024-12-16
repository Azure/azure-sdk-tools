import { ChangeDetectorRef, Component, OnInit, ViewChild } from '@angular/core';
import { ActivatedRoute, Params, Router } from '@angular/router';
import { MenuItem, TreeNode } from 'primeng/api';
import { Subject, take, takeUntil } from 'rxjs';
import { CodeLineRowNavigationDirection, getLanguageCssSafeName } from 'src/app/_helpers/common-helpers';
import { getQueryParams } from 'src/app/_helpers/router-helpers';
import { Review } from 'src/app/_models/review';
import { APIRevision, ApiTreeBuilderData } from 'src/app/_models/revision';
import { ReviewsService } from 'src/app/_services/reviews/reviews.service';
import { APIRevisionsService } from 'src/app/_services/revisions/revisions.service';
import { UserProfileService } from 'src/app/_services/user-profile/user-profile.service';
import { WorkerService } from 'src/app/_services/worker/worker.service';
import { CodePanelComponent } from '../code-panel/code-panel.component';
import { CommentsService } from 'src/app/_services/comments/comments.service';
import { ACTIVE_API_REVISION_ID_QUERY_PARAM, DIFF_API_REVISION_ID_QUERY_PARAM, DIFF_STYLE_QUERY_PARAM, REVIEW_ID_ROUTE_PARAM, SCROLL_TO_NODE_QUERY_PARAM } from 'src/app/_helpers/router-helpers';
import { CodePanelData, CodePanelRowData, CodePanelRowDatatype } from 'src/app/_models/codePanelModels';
import { UserProfile } from 'src/app/_models/userProfile';
import { ReviewPageWorkerMessageDirective } from 'src/app/_models/insertCodePanelRowDataMessage';
import { CommentItemModel, CommentType } from 'src/app/_models/commentItemModel';
import { SignalRService } from 'src/app/_services/signal-r/signal-r.service';
import { SamplesRevisionService } from 'src/app/_services/samples/samples.service';
import { SamplesRevision } from 'src/app/_models/samples';
import { CodeLineSearchInfo } from 'src/app/_models/codeLineSearchInfo';

@Component({
  selector: 'app-review-page',
  templateUrl: './review-page.component.html',
  styleUrls: ['./review-page.component.scss']
})
export class ReviewPageComponent implements OnInit {
  @ViewChild(CodePanelComponent) codePanelComponent!: CodePanelComponent;

  reviewId : string | null = null;
  activeApiRevisionId : string | null = null;
  diffApiRevisionId : string | null = null;
  diffStyle : string | null = null;

  userProfile : UserProfile | undefined;
  review : Review | undefined = undefined;
  apiRevisions: APIRevision[] = [];
  comments: CommentItemModel[] = [];
  activeAPIRevision : APIRevision | undefined = undefined;
  diffAPIRevision : APIRevision | undefined = undefined;
  latestSampleRevision: SamplesRevision | undefined = undefined;
  revisionSidePanel : boolean | undefined = undefined;
  conversationSidePanel : boolean | undefined = undefined;
  reviewPageNavigation : TreeNode[] = [];
  language: string | undefined;
  languageSafeName: string | undefined;
  scrollToNodeIdHashed : string | undefined;
  scrollToNodeId : string | undefined = undefined;
  showLineNumbers : boolean = true;
  preferredApprovers : string[] = [];
  hasFatalDiagnostics : boolean = false;
  hasActiveConversation : boolean = false;
  codeLineSearchInfo : CodeLineSearchInfo = new CodeLineSearchInfo();
  numberOfActiveConversation : number = 0;
  hasHiddenAPIs : boolean = false;
  hasHiddenAPIThatIsDiff : boolean = false;
  loadFailed : boolean = false;

  showLeftNavigation : boolean = true;
  showPageOptions : boolean = true;
  leftNavigationPanelSize = 14;
  pageOptionsPanelSize = 16;
  panelSizes = [this.leftNavigationPanelSize, 70, this.pageOptionsPanelSize];
  minSizes = [0.1, 1, 0.1];

  codePanelData: CodePanelData | null = null;
  codePanelRowData: CodePanelRowData[] = [];
  apiRevisionPageSize = 50;
  lastNodeIdUnhashedDiscarded = '';

  codeLineSearchText: string | undefined = undefined;
  codeLineNavigationDirection: number | undefined = undefined;

  private destroy$ = new Subject<void>();
  private destroyLoadAPIRevision$ : Subject<void>  | null = null;
  private destroyApiTreeBuilder$ : Subject<void>  | null = null;

  sideMenu: MenuItem[] | undefined;

  constructor(private route: ActivatedRoute, private router: Router, private apiRevisionsService: APIRevisionsService,
    private reviewsService: ReviewsService, private workerService: WorkerService, private changeDetectorRef: ChangeDetectorRef,
    private userProfileService: UserProfileService, private commentsService: CommentsService, private signalRService: SignalRService,
    private samplesRevisionService: SamplesRevisionService) {}

  ngOnInit() {
    this.reviewId = this.route.snapshot.paramMap.get(REVIEW_ID_ROUTE_PARAM);

    this.userProfileService.getUserProfile().subscribe(
      (userProfile : any) => {
        this.userProfile = userProfile;
        if (this.userProfile?.preferences.hideLeftNavigation) {
          this.showLeftNavigation = false;
          this.updateLeftPanelSize();
        }

        if (this.userProfile?.preferences.hideReviewPageOptions) {
          this.showPageOptions = false;
          this.updateRightPanelSize();
        }

        if(this.userProfile?.preferences.hideLineNumbers) {
          this.showLineNumbers = false;
        }
      });
      
    this.route.queryParams.pipe(takeUntil(this.destroy$)).subscribe(params => {
      const navigationState = this.router.getCurrentNavigation()?.extras.state;
      if (!navigationState || !navigationState['skipStateUpdate']) {
        this.updateStateBasedOnQueryParams(params);
      }
    });

    this.loadReview(this.reviewId!);
    this.loadPreferredApprovers(this.reviewId!);
    this.loadAPIRevisions(0, this.apiRevisionPageSize);
    this.loadComments();
    this.createSideMenu();
    this.handleRealTimeReviewUpdates();
    this.handleRealTimeAPIRevisionUpdates();
    this.loadLatestSampleRevision(this.reviewId!);
  }

  createSideMenu() {
    this.sideMenu = [
      {
          icon: 'bi bi-clock-history',
          tooltip: 'Revisions',
          command: () => { this.revisionSidePanel = !this.revisionSidePanel; }
      },
      {
        icon: 'bi bi-chat-left-dots',
        tooltip: 'Conversations',
        badge: (this.numberOfActiveConversation > 0) ? this.numberOfActiveConversation.toString() : undefined,
        command: () => { this.conversationSidePanel = !this.conversationSidePanel; }
      },
      {
        icon: 'bi bi-puzzle',
        tooltip: 'Samples',
        command: () => {
          if (this.latestSampleRevision) {          
            this.router.navigate(['/samples', this.reviewId],
            { queryParams: { activeSamplesRevisionId: this.latestSampleRevision?.id } });
          }
          else {
            this.router.navigate([`/samples/${this.reviewId}`])
          }
        }
      }
    ];
  }

  updateStateBasedOnQueryParams(params: Params) {
    this.activeApiRevisionId = params[ACTIVE_API_REVISION_ID_QUERY_PARAM];
    this.activeAPIRevision = this.apiRevisions.filter(x => x.id === this.activeApiRevisionId)[0];
    this.diffApiRevisionId = params[DIFF_API_REVISION_ID_QUERY_PARAM];
    this.diffAPIRevision = (this.diffApiRevisionId) ? this.apiRevisions.filter(x => x.id === this.diffApiRevisionId)[0] : undefined;
    this.diffStyle = params[DIFF_STYLE_QUERY_PARAM];
    this.scrollToNodeId = params[SCROLL_TO_NODE_QUERY_PARAM];
    this.reviewPageNavigation = [];
    this.codePanelRowData = [];
    this.codePanelData = null;
    this.loadFailed = false;
    this.changeDetectorRef.detectChanges();
    this.workerService.startWorker().then(() => {
      this.registerWorkerEventHandler();
      this.loadReviewContent(this.reviewId!, this.activeApiRevisionId, this.diffApiRevisionId);
    });
  }

  registerWorkerEventHandler() {
    // Ensure existing subscription is destroyed
    this.destroyApiTreeBuilder$?.next();
    this.destroyApiTreeBuilder$?.complete();
    this.destroyApiTreeBuilder$ = new Subject<void>();

    this.workerService.onMessageFromApiTreeBuilder().pipe(takeUntil(this.destroyApiTreeBuilder$)).subscribe(data => {
      if (data.directive === ReviewPageWorkerMessageDirective.CreatePageNavigation) {
        this.reviewPageNavigation = data.payload as TreeNode[];
      }

      if (data.directive === ReviewPageWorkerMessageDirective.UpdateCodePanelRowData) {
        this.codePanelRowData = data.payload as CodePanelRowData[];
        this.checkForFatalDiagnostics();
      }

      if (data.directive === ReviewPageWorkerMessageDirective.SetHasHiddenAPIFlag) {
        this.hasHiddenAPIs = data.payload as boolean;
      }

      if (data.directive === ReviewPageWorkerMessageDirective.UpdateCodePanelData) {
        this.codePanelData = data.payload as CodePanelData;
        this.hasHiddenAPIThatIsDiff = this.codePanelData.hasHiddenAPIThatIsDiff;
        this.workerService.terminateWorker();
      }
    });
  }

  loadReviewContent(reviewId: string, activeApiRevisionId: string | null = null, diffApiRevisionId: string | null = null) {
    this.reviewsService.getReviewContent(reviewId, activeApiRevisionId, diffApiRevisionId)
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: (response: ArrayBuffer) => {
          const apiTreeBuilderData : ApiTreeBuilderData = {
            diffStyle: this.diffStyle!,
            showDocumentation: this.userProfile?.preferences.showDocumentation ?? false,
            showComments: this.userProfile?.preferences.showComments ?? true,
            showSystemComments: this.userProfile?.preferences.showSystemComments ?? true,
            showHiddenApis: this.userProfile?.preferences.showHiddenApis ?? false
          };
          // Passing ArrayBufer to worker is way faster than passing object
          this.workerService.postToApiTreeBuilder(response, apiTreeBuilderData);
        },
        error: (error: any) => {
          this.loadFailed = true;
        }
      });
  }

  loadReview(reviewId: string) {
    this.reviewsService.getReview(reviewId)
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: (review: Review) => {
          this.review = review;
        }
      });
  }

  loadPreferredApprovers(reviewId: string) {
    this.reviewsService.getPreferredApprovers(reviewId)
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: (preferredApprovers: string[]) => {
          this.preferredApprovers = preferredApprovers;
        }
      });
  }

  loadAPIRevisions(noOfItemsRead : number, pageSize: number) {
    // Ensure existing subscription is destroyed
    this.destroyLoadAPIRevision$?.next();
    this.destroyLoadAPIRevision$?.complete();
    this.destroyLoadAPIRevision$ = new Subject<void>();

    this.apiRevisionsService.getAPIRevisions(noOfItemsRead, pageSize, this.reviewId!, undefined, undefined, 
      undefined, "createdOn", undefined, undefined, undefined, true)
      .pipe(takeUntil(this.destroyLoadAPIRevision$)).subscribe({
        next: (response: any) => {
          this.apiRevisions = response.result;
          if (this.apiRevisions.length > 0) {
            this.language = this.apiRevisions[0].language;
            this.languageSafeName = getLanguageCssSafeName(this.language);
            this.activeAPIRevision = this.apiRevisions.filter(x => x.id === this.activeApiRevisionId)[0];
            if (this.diffApiRevisionId) {
              this.diffAPIRevision = this.apiRevisions.filter(x => x.id === this.diffApiRevisionId)[0];
            }
          }
        }
      });
  }

  loadComments() {
    this.commentsService.getComments(this.reviewId!, CommentType.APIRevision)
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: (comments: CommentItemModel[]) => {
          this.comments = comments;
        }
      });
  }

  loadLatestSampleRevision(reviewId: string) {
    this.samplesRevisionService.getLatestSampleRevision(reviewId)
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: (sampleRevision: SamplesRevision | undefined) => {
          this.latestSampleRevision = sampleRevision;
        }
    });
  }

  handlePageOptionsEmitter(showPageOptions: boolean) {
    this.userProfile!.preferences.hideReviewPageOptions = !showPageOptions;
    this.userProfileService.updateUserPrefernece(this.userProfile!.preferences).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.updateRightPanelSize();
      }
    });
  }

  handleDiffStyleEmitter(state: string) {
    let newQueryParams = getQueryParams(this.route);
    newQueryParams[DIFF_STYLE_QUERY_PARAM] = state;
    this.router.navigate([], { queryParams: newQueryParams });
  }

  handleShowCommentsEmitter(state: boolean) {
    let userPreferenceModel = this.userProfile?.preferences;
    userPreferenceModel!.showComments = state;
    this.userProfileService.updateUserPrefernece(userPreferenceModel!).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        if (userPreferenceModel!.showComments) {
          this.codePanelComponent?.insertRowTypeIntoScroller(CodePanelRowDatatype.CommentThread);
        }
        else {
          this.codePanelComponent?.removeRowTypeFromScroller(CodePanelRowDatatype.CommentThread);
        }
      }
    });
  }

  handleShowSystemCommentsEmitter(state: boolean) {
    let userPreferenceModel = this.userProfile?.preferences;
    userPreferenceModel!.showSystemComments = state;
    this.userProfileService.updateUserPrefernece(userPreferenceModel!).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        if (userPreferenceModel!.showSystemComments) {
          this.codePanelComponent?.insertRowTypeIntoScroller(CodePanelRowDatatype.Diagnostics);
        }
        else {
          this.codePanelComponent?.removeRowTypeFromScroller(CodePanelRowDatatype.Diagnostics);
        }
      }
    });
  }

  handleShowDocumentationEmitter(state: boolean) {
    let userPreferenceModel = this.userProfile?.preferences;
    userPreferenceModel!.showDocumentation = state;
    this.userProfileService.updateUserPrefernece(userPreferenceModel!).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        if (userPreferenceModel!.showDocumentation) {
          this.codePanelComponent?.insertRowTypeIntoScroller(CodePanelRowDatatype.Documentation);
        }
        else {
          this.codePanelComponent?.removeRowTypeFromScroller(CodePanelRowDatatype.Documentation);
        }
      }
    });
  }

  handleShowLeftNavigationEmitter(state: boolean) {
    this.userProfile!.preferences.hideLeftNavigation = !state
    this.userProfileService.updateUserPrefernece(this.userProfile!.preferences).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.updateLeftPanelSize();
      }
    });
  }

  updateLeftPanelSize() {
    const panelSize = new Array(3);
    if (this.userProfile!.preferences.hideLeftNavigation) {
      this.showLeftNavigation = false;
      panelSize[0] = 0.1;
    } else {
      this.showLeftNavigation = true;
      panelSize[0] = this.leftNavigationPanelSize;
    }
    panelSize[2] = this.panelSizes[2];
    panelSize[1] = 100 - (panelSize[0] + panelSize[2]);
    this.panelSizes = panelSize;
  }

  updateRightPanelSize() {
    const panelSize = new Array(3);
    if  (this.userProfile!.preferences.hideReviewPageOptions) {
      this.showPageOptions = false;
      panelSize[2] = 0.1;
    } else {
      this.showPageOptions = true;
      panelSize[2] = this.pageOptionsPanelSize;
    }
    panelSize[0] = this.panelSizes[0];
    panelSize[1] = 100 - (panelSize[0] + panelSize[2]);
    this.panelSizes = panelSize;
  }

  handleSplitterResizeEnd(event: any) {
    if (event.sizes[0] > 5) {
      this.userProfile!.preferences.hideLeftNavigation = false;
    } else {
      this.userProfile!.preferences.hideLeftNavigation = true;
      this.updateLeftPanelSize();
    }

    if (event.sizes[2] > 5) {
      this.userProfile!.preferences.hideReviewPageOptions = false;
    } else {
      this.userProfile!.preferences.hideReviewPageOptions = true;
      this.updateRightPanelSize();
    }
    this.userProfileService.updateUserPrefernece(this.userProfile!.preferences).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.showLeftNavigation = !this.userProfile!.preferences.hideLeftNavigation;
        this.showPageOptions = !this.userProfile!.preferences.hideReviewPageOptions;

        // need this to trigger change detection
        const userProfile : UserProfile = {
          userName: this.userProfile!.userName,
          email: this.userProfile!.email,
          languages: this.userProfile!.languages,
          preferences: this.userProfile!.preferences
        };
        this.userProfile = userProfile;
      }
    });
  }

  handleShowLineNumbersEmitter(state: boolean) {
    let userPreferenceModel = this.userProfile?.preferences;
    userPreferenceModel!.hideLineNumbers = !state;
    this.userProfileService.updateUserPrefernece(userPreferenceModel!).pipe(takeUntil(this.destroy$)).subscribe(() => {
      this.showLineNumbers = !userPreferenceModel!.hideLineNumbers;
    });
  }

  handleNavTreeNodeEmmitter(nodeIdHashed: string) {
    this.scrollToNodeIdHashed = nodeIdHashed;
  }

  handleMarkAsViewedEmitter(state: boolean) {
    this.apiRevisionsService.toggleAPIRevisionViewedByForUser(this.activeApiRevisionId!, state).pipe(take(1)).subscribe({
      next: (apiRevision: APIRevision) => {
        this.activeAPIRevision = apiRevision;
        const activeAPIRevisionIndex = this.apiRevisions.findIndex(x => x.id === this.activeAPIRevision!.id);
        this.apiRevisions[activeAPIRevisionIndex] = this.activeAPIRevision!;
      } 
    });
  }

  handleSubscribeEmitter(state: boolean) {
    this.reviewsService.toggleReviewSubscriptionByUser(this.reviewId!, state).pipe(take(1)).subscribe();
  }

  handleApiRevisionApprovalEmitter(value: boolean) {
    if (value) {
      this.apiRevisionsService.toggleAPIRevisionApproval(this.reviewId!, this.activeApiRevisionId!).pipe(take(1)).subscribe({
        next: (apiRevision: APIRevision) => {
          this.activeAPIRevision = apiRevision;
          const activeAPIRevisionIndex = this.apiRevisions.findIndex(x => x.id === this.activeAPIRevision!.id);
          this.apiRevisions[activeAPIRevisionIndex] = this.activeAPIRevision!;
        }
      });
    }
  }

  handleReviewApprovalEmitter(value: boolean) {
    if (value) {
      this.reviewsService.toggleReviewApproval(this.reviewId!, this.activeApiRevisionId!).pipe(take(1)).subscribe({
        next: (review: Review) => {
          this.review = review;
        }
      });
    }
  }

  handleShowHiddenAPIEmitter(state: boolean) {
    let userPreferenceModel = this.userProfile?.preferences;
    userPreferenceModel!.showHiddenApis = state;
    this.userProfileService.updateUserPrefernece(userPreferenceModel!).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        const currentParams = this.route.snapshot.queryParams;
        this.updateStateBasedOnQueryParams(currentParams);
      }
    });
  }

  handleDisableCodeLinesLazyLoadingEmitter(state: boolean) {
    let userPreferenceModel = this.userProfile?.preferences;
    userPreferenceModel!.disableCodeLinesLazyLoading = state;
    this.userProfileService.updateUserPrefernece(userPreferenceModel!).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        const currentParams = this.route.snapshot.queryParams;
        this.updateStateBasedOnQueryParams(currentParams);
      }
    });
  }

  handleCommentThreadNavaigationEmitter(direction: CodeLineRowNavigationDirection) {
    this.codePanelComponent.navigateToCommentThread(direction);
  }

  handleDiffNavaigationEmitter(direction: CodeLineRowNavigationDirection) {
    this.codePanelComponent.navigateToDiffNode(direction);
  }

  handleCopyReviewTextEmitter(event: boolean) {
    this.codePanelComponent.copyReviewTextToClipBoard();
  }
  
  handleCodeLineSearchTextEmitter(searchText: string) {
    this.codeLineSearchText = searchText;
  }

  handleCodeLineSearchNaviationEmmiter(direction: number) {
    this.codeLineNavigationDirection = direction;
  }

  handleHasActiveConversationEmitter(value: boolean) {
    this.hasActiveConversation = value;
  }

  handleCodeLineSearchInfoEmitter(value: CodeLineSearchInfo) {
    this.codeLineSearchInfo = value;
  }

  handleNumberOfActiveThreadsEmitter(value: number) {
    this.numberOfActiveConversation = value;
    this.createSideMenu();
  }

  handleScrollToNodeEmitter (value: string) {
    this.conversationSidePanel = false;
    this.codePanelComponent.scrollToNode(undefined, value);
  }

  handleRealTimeReviewUpdates() {
    this.signalRService.onReviewUpdates().pipe(takeUntil(this.destroy$)).subscribe({
      next: (updatedReview: Review) => {
        if (updatedReview.id === this.reviewId) {
          this.review = updatedReview;
        }
      }
    });
  }

  handleRealTimeAPIRevisionUpdates() {
    this.signalRService.onAPIRevisionUpdates().pipe(takeUntil(this.destroy$)).subscribe({
      next: (updatedAPIRevision: APIRevision) => {
        if (updatedAPIRevision.reviewId === this.reviewId) {
          const apiRevisionIndex = this.apiRevisions.findIndex(x => x.id === updatedAPIRevision.id);
          if (apiRevisionIndex > -1) {
            this.apiRevisions[apiRevisionIndex] = updatedAPIRevision;
          }

          if (updatedAPIRevision.id === this.activeApiRevisionId) {
            this.activeAPIRevision = updatedAPIRevision;
          }
        }
      }
    });
  }

  checkForFatalDiagnostics() {
    for (const rowData of this.codePanelRowData) {
      if (rowData.diagnostics && rowData.diagnostics.level === 'fatal') {
        this.hasFatalDiagnostics = true;
        break;
      }
    }
  }
  
  ngOnDestroy() {
    this.workerService.terminateWorker();
    this.destroy$.next();
    this.destroy$.complete();
    this.destroyLoadAPIRevision$?.next();
    this.destroyLoadAPIRevision$?.complete();
    this.destroyApiTreeBuilder$?.next();
    this.destroyApiTreeBuilder$?.complete();
  }
}
