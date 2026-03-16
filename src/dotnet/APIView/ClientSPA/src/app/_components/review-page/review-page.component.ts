import { ChangeDetectorRef, Component, Input, OnInit, OnDestroy, ViewChild } from '@angular/core';
import { ActivatedRoute, Params, Router } from '@angular/router';
import { Title } from '@angular/platform-browser';
import { MenuItem, TreeNode } from 'primeng/api';
import { concatMap, EMPTY, from, Observable, Subject, take, takeUntil, tap } from 'rxjs';
import { CodeLineRowNavigationDirection, getLanguageCssSafeName } from 'src/app/_helpers/common-helpers';
import { getQueryParams } from 'src/app/_helpers/router-helpers';
import { Review, PackageType } from 'src/app/_models/review';
import { APIRevision, APIRevisionGroupedByLanguage, ApiTreeBuilderData } from 'src/app/_models/revision';
import { ReviewsService } from 'src/app/_services/reviews/reviews.service';
import { APIRevisionsService } from 'src/app/_services/revisions/revisions.service';
import { UserProfileService } from 'src/app/_services/user-profile/user-profile.service';
import { WorkerService } from 'src/app/_services/worker/worker.service';
import { CodePanelComponent } from '../code-panel/code-panel.component';
import { ReviewPageOptionsComponent } from '../review-page-options/review-page-options.component';
import { CommentsService } from 'src/app/_services/comments/comments.service';
import { CommentRelationHelper } from 'src/app/_helpers/comment-relation.helper';
import { ACTIVE_API_REVISION_ID_QUERY_PARAM, DIFF_API_REVISION_ID_QUERY_PARAM, DIFF_STYLE_QUERY_PARAM, REVIEW_ID_ROUTE_PARAM, SCROLL_TO_NODE_QUERY_PARAM } from 'src/app/_helpers/router-helpers';
import { CodePanelData, CodePanelRowData, CodePanelRowDatatype, CrossLanguageContentDto } from 'src/app/_models/codePanelModels';
import { UserProfile } from 'src/app/_models/userProfile';
import { ReviewPageWorkerMessageDirective } from 'src/app/_models/insertCodePanelRowDataMessage';
import { CommentItemModel, CommentType, CommentSeverity, CommentSource } from 'src/app/_models/commentItemModel';
import { SignalRService } from 'src/app/_services/signal-r/signal-r.service';
import { SamplesRevisionService } from 'src/app/_services/samples/samples.service';
import { SamplesRevision } from 'src/app/_models/samples';
import { CodeLineSearchInfo } from 'src/app/_models/codeLineSearchInfo';
import { HttpResponse } from '@angular/common/http';
import { environment } from 'src/environments/environment';
import { NotificationsService } from 'src/app/_services/notifications/notifications.service';
import { SiteNotification } from 'src/app/_models/notificationsModel';
import { ReviewContextService } from 'src/app/_services/review-context/review-context.service';
import { PermissionsService } from 'src/app/_services/permissions/permissions.service';

@Component({
    selector: 'app-review-page',
    templateUrl: './review-page.component.html',
    styleUrls: ['./review-page.component.scss'],
    standalone: false
})
export class ReviewPageComponent implements OnInit, OnDestroy {
  @ViewChild(CodePanelComponent) codePanelComponent!: CodePanelComponent;
  @ViewChild(ReviewPageOptionsComponent) reviewPageOptionsComponent!: ReviewPageOptionsComponent;

  reviewId: string | null = null;
  activeApiRevisionId: string | null = null;
  diffApiRevisionId: string | null = null;
  diffStyle: string | null = null;

  assetsPath: string = environment.assetsPath;

  userProfile: UserProfile | undefined;
  review: Review | undefined = undefined;
  apiRevisions: APIRevision[] = [];
  crossLanguageAPIRevisions: APIRevisionGroupedByLanguage[] = [];
  comments: CommentItemModel[] = [];
  activeAPIRevision: APIRevision | undefined = undefined;
  diffAPIRevision: APIRevision | undefined = undefined;
  latestSampleRevision: SamplesRevision | undefined = undefined;
  revisionSidePanel: boolean | undefined = undefined;
  crosslanguageRevisionSidePanel: boolean | undefined = undefined;
  conversationSidePanel: boolean | undefined = undefined;
  reviewPageNavigation: TreeNode[] = [];
  language: string | undefined;
  languageSafeName: string | undefined;
  scrollToNodeIdHashed: Subject<string> = new Subject<string>();
  scrollToNodeId: string | undefined = undefined;
  showLineNumbers: boolean = true;
  hasFatalDiagnostics: boolean = false;
  hasActiveConversation: boolean = false;
  codeLineSearchInfo: CodeLineSearchInfo | undefined;
  numberOfActiveConversation: number = 0;
  hasHiddenAPIs: boolean = false;
  hasHiddenAPIThatIsDiff: boolean = false;
  loadFailed: boolean = false;
  loadFailedMessage: string = "API-Revision Content Load Failed...";
  loadingMessage: string | undefined = undefined;

  showLeftNavigation: boolean = true;
  showPageOptions: boolean = true;
  leftNavigationPanelSize = 14;
  pageOptionsPanelSize = 16;
  panelSizes = [this.leftNavigationPanelSize, 70, this.pageOptionsPanelSize];
  minSizes = [0.1, 1, 0.1];

  codePanelData: CodePanelData | null = null;
  codePanelRowData: CodePanelRowData[] = [];
  crossLanguageRowData: CrossLanguageContentDto[] = [];
  apiRevisionPageSize = 200;
  lastNodeIdUnhashedDiscarded = '';

  codeLineSearchText: string | undefined = undefined;

  private destroy$ = new Subject<void>();
  private destroyLoadAPIRevision$: Subject<void> | null = null;
  private destroyApiTreeBuilder$: Subject<void> | null = null;

  sideMenu: MenuItem[] | undefined;

  constructor(private route: ActivatedRoute, private router: Router, private apiRevisionsService: APIRevisionsService,
    private reviewsService: ReviewsService, private workerService: WorkerService, private changeDetectorRef: ChangeDetectorRef,
    private userProfileService: UserProfileService, private commentsService: CommentsService, private signalRService: SignalRService,
    private samplesRevisionService: SamplesRevisionService, private titleService: Title, private notificationsService: NotificationsService,
    private reviewContextService: ReviewContextService, private permissionsService: PermissionsService) { }

  ngOnInit() {
    this.reviewId = this.route.snapshot.paramMap.get(REVIEW_ID_ROUTE_PARAM);

    this.userProfileService.getUserProfile().subscribe(
      (userProfile: any) => {
        this.userProfile = userProfile;
        if (this.userProfile?.preferences.hideLeftNavigation) {
          this.showLeftNavigation = false;
          this.updateLeftPanelSize();
        }

        if (this.userProfile?.preferences.hideReviewPageOptions) {
          this.showPageOptions = false;
          this.updateRightPanelSize();
        }

        if (this.userProfile?.preferences.hideLineNumbers) {
          this.showLineNumbers = false;
        }
      });
    this.route.queryParams.pipe(takeUntil(this.destroy$)).subscribe(params => {
      const navigationState = this.router.currentNavigation()?.extras.state;
      if (!navigationState || !navigationState['skipStateUpdate']) {
        this.updateStateBasedOnQueryParams(params);
      }
    });

    this.loadReview(this.reviewId!);
    this.loadAPIRevisions(0, this.apiRevisionPageSize);
    this.handleRealTimeReviewUpdates();
    this.handleRealTimeAPIRevisionUpdates();
    this.loadLatestSampleRevision(this.reviewId!);
  }

  createSideMenu() {
    const menu: MenuItem[] = [
      {
        icon: 'bi bi-clock-history',
        tooltip: 'Revisions',
        command: () => {
          if (this.getLoadingStatus() === 'completed') {
            this.revisionSidePanel = !this.revisionSidePanel;
          }
        }
      }
    ];

    if (this.activeAPIRevision?.files[0].crossLanguagePackageId && this.crossLanguageAPIRevisions.length > 0) {
      menu.push({
        icon: 'bi bi-arrow-left-right',
        tooltip: 'Cross Language',
        command: () => {
          if (this.getLoadingStatus() === 'completed') {
            this.crosslanguageRevisionSidePanel = !this.crosslanguageRevisionSidePanel;
          }
        }
      });
    }
    menu.push(...[
      {
        id: 'conversations',
        icon: 'bi bi-chat-left-dots',
        tooltip: (this.numberOfActiveConversation > 0) ? `Conversations (${this.numberOfActiveConversation} unresolved)` : 'Conversations',
        badge: (this.numberOfActiveConversation > 0) ? this.numberOfActiveConversation.toString() : undefined,
        command: () => {
          if (this.getLoadingStatus() === 'completed') {
            this.conversationSidePanel = !this.conversationSidePanel;
          }
        }
      },
      {
        icon: 'bi bi-puzzle',
        tooltip: 'Samples',
        command: () => {
          const queryParams: any = {};
          if (this.latestSampleRevision) {
            queryParams['activeSamplesRevisionId'] = this.latestSampleRevision?.id;
          }
          if (this.activeApiRevisionId) {
            queryParams['activeApiRevisionId'] = this.activeApiRevisionId;
          }
          if (this.diffApiRevisionId) {
            queryParams['diffApiRevisionId'] = this.diffApiRevisionId;
          }
          this.router.navigate(['/samples', this.reviewId], { queryParams: queryParams });
        }
      }
    ]);
    this.sideMenu = menu;
    this.changeDetectorRef.detectChanges();
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
    this.comments = [];
    this.loadFailed = false;

    // Clear the conversations badge immediately — the count will be inaccurate
    // until the code panel finishes loading and diagnostics are synced.
    this.numberOfActiveConversation = 0;
    if (this.sideMenu) {
      const conversationsItem = this.sideMenu.find(item => item.id === 'conversations');
      if (conversationsItem) {
        conversationsItem.badge = undefined;
        conversationsItem.tooltip = 'Conversations';
        this.sideMenu = [...this.sideMenu];
      }
    }

    // Set loading message if diagnostics need migration (old revisions without hash)
    this.loadingMessage = (this.activeAPIRevision && !this.activeAPIRevision.diagnosticsHash)
      ? 'Processing diagnostics...'
      : undefined;

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
        this.processEmbeddedComments();
        this.workerService.terminateWorker();
        this.loadComments();
      }
    });
  }

  loadReviewContent(reviewId: string, activeApiRevisionId: string | null = null, diffApiRevisionId: string | null = null) {
    this.reviewsService.getReviewContent(reviewId, activeApiRevisionId, diffApiRevisionId)
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: (response: HttpResponse<ArrayBuffer>) => {
          if (this.updateLoadingStateBasedOnReviewDeletionStatus()) {
            return;
          }
          if (response.status == 204) {
            this.loadFailed = true;
            this.loadFailedMessage = "API-Revision Content Not Found. The";
            this.loadFailedMessage += (diffApiRevisionId) ? " active and/or diff API-Revision(s)" : " active API-Revision";
            this.loadFailedMessage += " may have been deleted.";
            return;
          } else if (response.status == 202) {
            const location = response.headers.get('location');
            this.loadFailed = true;
            this.loadFailedMessage = `API-Revision content is being generated at <a href="${location}">${location}</a></br>`
            this.loadFailedMessage += "Please refresh this page after few minutes to see generated API review.";
            return;
          }
          else {
            const apiTreeBuilderData: ApiTreeBuilderData = {
              diffStyle: this.diffStyle!,
              showDocumentation: this.userProfile?.preferences.showDocumentation ?? false,
              showComments: this.userProfile?.preferences.showComments ?? true,
              showSystemComments: this.userProfile?.preferences.showSystemComments ?? true,
              showHiddenApis: this.userProfile?.preferences.showHiddenApis ?? false
            };
            // Passing ArrayBufer to worker is way faster than passing object
            this.workerService.postToApiTreeBuilder(response.body, apiTreeBuilderData);
          }
          this.createSideMenu();
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
          this.updateLoadingStateBasedOnReviewDeletionStatus();
          this.updatePageTitle();
          if (review.language) {
            this.loadApproversForLanguage(review.language);
          }
        },
        error: (error) => {
          this.loadFailed = true;
          this.loadFailedMessage = "Failed to load review. Please refresh the page or try again later.";
        }
      });
  }

  loadApproversForLanguage(language: string) {
    this.permissionsService.getApproversForLanguage(language)
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: (approvers: string[]) => {
          this.reviewContextService.setLanguageApprovers(approvers);
        }
      });
  }

  loadAPIRevisions(noOfItemsRead: number, pageSize: number) {
    // Ensure existing subscription is destroyed
    this.destroyLoadAPIRevision$?.next();
    this.destroyLoadAPIRevision$?.complete();
    this.destroyLoadAPIRevision$ = new Subject<void>();

    // Ensures that the pertinent apirevisons are loaded regardless of page size limits
    const pageRevisions: string[] = [this.activeApiRevisionId!];
    if (this.diffApiRevisionId) {
      pageRevisions.push(this.diffApiRevisionId);
    }

    this.apiRevisionsService.getAPIRevisions(noOfItemsRead, pageSize, this.reviewId!, undefined, undefined,
      undefined, "createdOn", undefined, undefined, undefined, true, pageRevisions)
      .pipe(
        takeUntil(this.destroyLoadAPIRevision$),
        concatMap((response: any) => {
          this.apiRevisions = response.result;
          if (this.apiRevisions.length > 0) {
            this.language = this.apiRevisions[0].language;
            this.languageSafeName = getLanguageCssSafeName(this.language);
            this.reviewContextService.setLanguage(this.language);
            this.activeAPIRevision = this.apiRevisions.filter(x => x.id === this.activeApiRevisionId)[0];
            if (this.diffApiRevisionId) {
              this.diffAPIRevision = this.apiRevisions.filter(x => x.id === this.diffApiRevisionId)[0];
            }
          }

          if (this.activeAPIRevision && this.activeAPIRevision.files[0].crossLanguagePackageId) {
            return this.apiRevisionsService.getCrossLanguageAPIRevisions(this.activeAPIRevision.files[0].crossLanguagePackageId);
          }
          return EMPTY
        }),
        concatMap((response: any) => {
          this.crossLanguageAPIRevisions = response.filter((c: APIRevisionGroupedByLanguage) => c.label !== this.language);
          this.createSideMenu();
          if (this.crossLanguageAPIRevisions.length > 0) {
            const itemsToProcess = this.crossLanguageAPIRevisions
              .filter(revision => revision.items.length > 0 && revision.items[0].files.length > 0)
              .map(revision => ({
                reviewId: revision.items[0].reviewId,
                apiRevisionId: revision.items[0].id,
                codeFileId: revision.items[0].files[0].fileId,
                packageVersion: revision.items[0].packageVersion,
                packageName: revision.items[0].packageName
              }));
            return from(itemsToProcess).pipe(
              concatMap((item: any) => this.reviewsService.getCrossLanguageContent(item.apiRevisionId, item.codeFileId).pipe(
                tap(response => {
                  response.reviewId = item.reviewId;
                  response.packageVersion = item.packageVersion;
                  response.packageName = item.packageName;
                  this.crossLanguageRowData.push(response);
                })
              ))
            );
          }
          return EMPTY;
        }),
      ).subscribe({
        next: (response: any) => {

        }
      });
  }

  loadComments() {
    this.commentsService.getComments(this.reviewId!, CommentType.APIRevision)
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: (comments: CommentItemModel[]) => {
          this.comments = comments;
          CommentRelationHelper.calculateRelatedComments(this.comments);
          this.processEmbeddedComments();
        }
      });
  }

  private processEmbeddedComments() {
    if (!this.codePanelData || !this.comments) {
        return;
    }

    Object.values(this.codePanelData.nodeMetaData).forEach((nodeData) => {
      if (nodeData.commentThread) {
        Object.values(nodeData.commentThread).forEach((commentThreads: any) => {
          let rows: any[] = [];
          if (Array.isArray(commentThreads)) {
            rows = commentThreads;
          } else if (commentThreads && typeof commentThreads === 'object') {
             if (commentThreads.type || commentThreads.comments) {
                 rows = [commentThreads];
             } else {
                 rows = Object.values(commentThreads);
             }
          }

          rows.forEach((commentThreadRow: any) => {
            if (commentThreadRow && commentThreadRow.comments) {
              commentThreadRow.comments.forEach((embeddedComment: any) => {
                const globalComment = this.comments.find(c => c.id === embeddedComment.id);
                if (globalComment) {
                  embeddedComment.hasRelatedComments = globalComment.hasRelatedComments;
                  embeddedComment.relatedCommentsCount = globalComment.relatedCommentsCount;
                }
              });
            }
          });
        });
      }
    });
    this.changeDetectorRef.detectChanges();
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
          this.codePanelComponent?.insertDiagnosticCommentThreads();
        } else {
          this.codePanelComponent?.removeDiagnosticCommentThreads();
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
    if (this.userProfile!.preferences.hideReviewPageOptions) {
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
        const userProfile: UserProfile = {
          userName: this.userProfile!.userName,
          email: this.userProfile!.email,
          languages: this.userProfile!.languages,
          preferences: this.userProfile!.preferences,
          permissions: this.userProfile!.permissions
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
    this.scrollToNodeIdHashed.next(nodeIdHashed);
  }

  handleSubscribeEmitter(state: boolean) {
    this.reviewsService.toggleReviewSubscriptionByUser(this.reviewId!, state).pipe(take(1)).subscribe();
  }

  handleApiRevisionApprovalEmitter(value: boolean) {
    if (value) {
      const approvalState = !this.activeAPIRevision?.approvers.includes(this.userProfile?.userName!);
      this.apiRevisionsService.toggleAPIRevisionApproval(this.reviewId!, this.activeApiRevisionId!, approvalState).pipe(take(1)).subscribe({
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
      this.reviewsService.toggleReviewApproval(this.reviewId!, this.activeApiRevisionId!, true).pipe(take(1)).subscribe({
        next: (review: Review) => {
          this.review = review;
        }
      });
    }
  }

  handleNamespaceApprovalEmitter(value: boolean) {
    if (value) {
      this.reviewsService.requestNamespaceReview(this.reviewId!, this.activeApiRevisionId!).pipe(take(1)).subscribe({
        next: (review: Review) => {
          this.review = review;
          // Reset loading state in the options component on success
          if (this.reviewPageOptionsComponent) {
            this.reviewPageOptionsComponent.resetNamespaceReviewLoadingState();
          }
          try {
            const notification = new SiteNotification(
              this.reviewId!,
              this.activeApiRevisionId!,
              'Namespace Review',
              'Namespace review has been requested successfully!',
              'success'
            );
            this.notificationsService.addNotification(notification);
          } catch (error) {
            // Fallback alert
            alert('Namespace review has been requested successfully!');
          }
        },
        error: (error) => {
          console.error('Error requesting namespace review:', error);
          // Reset loading state in the options component on error
          if (this.reviewPageOptionsComponent) {
            this.reviewPageOptionsComponent.resetNamespaceReviewLoadingState();
          }
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

  handleCommentThreadNavigationEmitter(direction: CodeLineRowNavigationDirection) {
    this.codePanelComponent.navigateToCommentThread(direction);
  }

  handleDiffNavigationEmitter(direction: CodeLineRowNavigationDirection) {
    this.codePanelComponent.navigateToDiffNode(direction);
  }

  handleCopyReviewTextEmitter(event: boolean) {
    this.codePanelComponent.copyReviewTextToClipBoard(event);
  }

  handleCodeLineSearchTextEmitter(searchText: string) {
    this.codeLineSearchText = searchText;
  }

  handleHasActiveConversationEmitter(value: boolean) {
    this.hasActiveConversation = value;
  }

  handleCodeLineSearchInfoEmitter(value: CodeLineSearchInfo) {
    setTimeout(() => {
      this.codeLineSearchInfo = (value) ? new CodeLineSearchInfo(value.currentMatch, value.totalMatchCount) : undefined;
    }, 0);
  }

  handleNumberOfActiveThreadsEmitter(value: number) {
    // Suppress the badge until the code panel has loaded. Before that point,
    // diagnostics may not be synced yet so the count would be inaccurate.
    if (!this.codePanelData) {
      return;
    }
    this.numberOfActiveConversation = value;
    if (this.sideMenu) {
      const conversationsItem = this.sideMenu.find(item => item.id === 'conversations');
      if (conversationsItem) {
        conversationsItem.badge = (value > 0) ? value.toString() : undefined;
        conversationsItem.tooltip = (value > 0) ? `Conversations (${value} unresolved)` : 'Conversations';
        this.sideMenu = [...this.sideMenu];
        this.changeDetectorRef.detectChanges();
      }
    } else {
      this.createSideMenu();
    }
  }

  handleScrollToNodeEmitter(value: string) {
    this.conversationSidePanel = false;
    this.codePanelComponent.scrollToNode(undefined, value);
  }

  handleDismissSidebarAndNavigate(event: { revisionId: string, elementId: string }) {
    this.conversationSidePanel = false;
    const currentParams = getQueryParams(this.route);
    this.router.navigate(['/review', this.reviewId], {
      queryParams: {
        ...currentParams,
        activeApiRevisionId: event.revisionId,
        nId: event.elementId
      }
    });
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

  handleCrossLangaugeAPIRevisionChange(value: APIRevision) {
    this.reviewsService.getCrossLanguageContent(value.id, value.files[0].fileId)
      .pipe(takeUntil(this.destroy$))
      .subscribe(response => {
        response.reviewId = value.reviewId;
        response.packageVersion = value.packageVersion;
        response.packageName = value.packageName;
        const cldto: CrossLanguageContentDto[] = [];
        this.crossLanguageRowData.forEach((dto: CrossLanguageContentDto) => {
          if (dto.apiRevisionId !== value.id) {
            cldto.push(dto);
          }
        });
        cldto.push(response)
        this.crossLanguageRowData = cldto;
      });
  }

  getLoadingStatus(): 'loading' | 'completed' | 'failed' {
    if (this.codePanelComponent?.isLoading) {
      return 'loading';
    }
    else {
      return (this.loadFailed) ? 'failed' : 'completed';
    }
  }

  checkForFatalDiagnostics() {
    for (const rowData of this.codePanelRowData) {
      // Check legacy diagnostic rows
      if (rowData.diagnostics && rowData.diagnostics.level === 'fatal') {
        this.hasFatalDiagnostics = true;
        break;
      }
      if (rowData.comments) {
        for (const comment of rowData.comments) {
          if (comment.commentSource === CommentSource.Diagnostic && comment.severity === CommentSeverity.MustFix) {
            this.hasFatalDiagnostics = true;
            break;
          }
        }
        if (this.hasFatalDiagnostics) break;
      }
    }
  }

  updateLoadingStateBasedOnReviewDeletionStatus(): boolean {
    if (this.review?.isDeleted) {
      this.loadFailed = true;
      this.loadFailedMessage = "Review has been deleted.";
      return true;
    }
    return false;
  }

  updatePageTitle() {
    if (this.review?.packageName) {
      this.titleService.setTitle(this.review.packageName);
    } else {
      this.titleService.setTitle('APIView');
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
    this.reviewContextService.clear();
  }
}
