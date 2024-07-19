import { ChangeDetectorRef, Component, OnInit, ViewChild } from '@angular/core';
import { ActivatedRoute, Params, Router } from '@angular/router';
import { MenuItem, TreeNode } from 'primeng/api';
import { Subject, Subscription, take, takeUntil, tap } from 'rxjs';
import { getLanguageCssSafeName } from 'src/app/_helpers/component-helpers';
import { getQueryParams } from 'src/app/_helpers/router-helpers';
import { UserProfile } from 'src/app/_models/auth_service_models';
import { Review } from 'src/app/_models/review';
import { APIRevision, ApiTreeBuilderData, CodePanelData, CodePanelRowData, CodePanelRowDatatype, CodePanelToggleableData, ReviewPageWorkerMessageDirective } from 'src/app/_models/revision';
import { ReviewsService } from 'src/app/_services/reviews/reviews.service';
import { RevisionsService } from 'src/app/_services/revisions/revisions.service';
import { UserProfileService } from 'src/app/_services/user-profile/user-profile.service';
import { WorkerService } from 'src/app/_services/worker/worker.service';
import { CodePanelComponent } from '../code-panel/code-panel.component';
import { CommentsService } from 'src/app/_services/comments/comments.service';
import { ACTIVE_API_REVISION_ID_QUERY_PARAM, DIFF_API_REVISION_ID_QUERY_PARAM, DIFF_STYLE_QUERY_PARAM, REVIEW_ID_ROUTE_PARAM, SCROLL_TO_NODE_QUERY_PARAM } from 'src/app/_helpers/literal-helpers';

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
  activeAPIRevision : APIRevision | undefined = undefined;
  diffAPIRevision : APIRevision | undefined = undefined;
  revisionSidePanel : boolean | undefined = undefined;
  reviewPageNavigation : TreeNode[] = [];
  language: string | undefined;
  languageSafeName: string | undefined;
  scrollToNodeIdHashed : string | undefined;
  scrollToNodeId : string | undefined = undefined;
  showLineNumbers : boolean = true;
  preferredApprovers : string[] = [];
  hasFatalDiagnostics : boolean = false;
  hasActiveConversation : boolean = false;
  hasHiddenAPIs : boolean = false;
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

  private destroy$ = new Subject<void>();
  private destroyLoadAPIRevision$ : Subject<void>  | null = null;
  private destroyApiTreeBuilder$ : Subject<void>  | null = null;

  sideMenu: MenuItem[] | undefined;

  constructor(private route: ActivatedRoute, private router: Router, private apiRevisionsService: RevisionsService,
    private reviewsService: ReviewsService, private workerService: WorkerService, private changeDetectorRef: ChangeDetectorRef,
    private userProfileService: UserProfileService, private commentsService: CommentsService) {}

  ngOnInit() {
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

    this.reviewId = this.route.snapshot.paramMap.get(REVIEW_ID_ROUTE_PARAM);

    this.loadReview(this.reviewId!);
    this.loadPreferredApprovers(this.reviewId!);
    this.loadAPIRevisions(0, this.apiRevisionPageSize);

    this.sideMenu = [
      {
          icon: 'bi bi-clock-history',
          command: () => { this.revisionSidePanel = !this.revisionSidePanel; }
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

  handleHasActiveConversationEmitter(value: boolean) {
    this.hasActiveConversation = value;
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
