import { ChangeDetectorRef, Component, ElementRef, Injector, Input, Renderer2, SimpleChange, ViewContainerRef } from '@angular/core';
import { ActivatedRoute, Params, Router } from '@angular/router';
import { Title } from '@angular/platform-browser';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MenuItem, MessageService } from 'primeng/api';
import { FileSelectEvent } from 'primeng/fileupload';
import { DialogModule } from 'primeng/dialog';
import { DrawerModule } from 'primeng/drawer';
import { InputTextModule } from 'primeng/inputtext';
import { Subject, take, takeUntil } from 'rxjs';
import { ACTIVE_SAMPLES_REVISION_ID_QUERY_PARAM, REVIEW_ID_ROUTE_PARAM, ACTIVE_API_REVISION_ID_QUERY_PARAM, DIFF_API_REVISION_ID_QUERY_PARAM, getQueryParams } from 'src/app/_helpers/router-helpers';
import { CodePanelRowData, CodePanelRowDatatype } from 'src/app/_models/codePanelModels';
import { CommentItemModel, CommentType } from 'src/app/_models/commentItemModel';
import { PaginatedResult } from 'src/app/_models/pagination';
import { Review } from 'src/app/_models/review';
import { APIRevision } from 'src/app/_models/revision';
import { SamplesRevision } from 'src/app/_models/samples';
import { UserProfile } from 'src/app/_models/userProfile';
import { CommentsService } from 'src/app/_services/comments/comments.service';
import { ConfigService } from 'src/app/_services/config/config.service';
import { ReviewsService } from 'src/app/_services/reviews/reviews.service';
import { APIRevisionsService } from 'src/app/_services/revisions/revisions.service';
import { SamplesRevisionService } from 'src/app/_services/samples/samples.service';
import { UserProfileService } from 'src/app/_services/user-profile/user-profile.service';
import { CommentThreadComponent } from '../shared/comment-thread/comment-thread.component';
import { CommentThreadUpdateAction, CommentUpdatesDto } from 'src/app/_dtos/commentThreadUpdateDto';
import { ReviewPageLayoutModule } from 'src/app/_modules/shared/review-page-layout.module';
import { CodeEditorComponent } from '../shared/code-editor/code-editor.component';
import { MarkdownToHtmlPipe } from 'src/app/_pipes/markdown-to-html.pipe';

@Component({
    selector: 'app-samples-page',
    templateUrl: './samples-page.component.html',
    styleUrls: ['./samples-page.component.scss'],
    standalone: true,
    imports: [
      CommonModule,
      FormsModule,
      DialogModule,
      DrawerModule,
      InputTextModule,
      ReviewPageLayoutModule,
      CodeEditorComponent,
      MarkdownToHtmlPipe,
    ]
})
export class SamplesPageComponent {
  SAMPLES_CONTENT_PLACEHOLDER = "<!--- Enter Markdown Formated Content --->";

  // Embedded mode inputs — when true, parent provides data and navigation uses query params
  @Input() embedded = false;
  @Input() embeddedReviewId: string | null = null;
  @Input() embeddedReview: Review | undefined;
  @Input() embeddedUserProfile: UserProfile | undefined;

  webAppUrl : string = this.configService.webAppUrl

  reviewId : string | null = null;
  activeSamplesRevisionId : string | null = null;
  activeApiRevisionId: string | null = null;
  diffApiRevisionId: string | null = null;
  activeSamplesRevision: SamplesRevision | undefined = undefined;
  review : Review | undefined = undefined;
  sideMenu: MenuItem[] | undefined;
  latestApiRevision: APIRevision | undefined = undefined;
  samplesRevisions: SamplesRevision[] = [];
  samplesRevisionTotalCount: number = 0;
  userProfile : UserProfile | undefined;
  comments: CommentItemModel[] = [];
  commentThreads: Map<string, CodePanelRowData> =  new Map<string, CodePanelRowData>;

  samplesContent: string | undefined = undefined;
  loadFailed : boolean = false;
  isLoading : boolean = true;
  commentsLoaded: boolean = false;
  commentableRegionsAdded : boolean = false;

  activeView: 'list' | 'detail' = 'list';
  sampleToDelete: SamplesRevision | null = null;

  samplesRevisionsPageSize = 50;

  samplesUpdateSidePanel = false
  samplesUpdateState: "add" | "edit" | undefined = undefined;

  addEditSamplesTitle : string = "";
  addEditSamplesContent : string = "";
  samplesUploadFile : File | undefined = undefined;
  showSamplesDeleteModal : boolean = false;

  deleteSamplesButton : string = "Delete";
  createSamplesButton : string = "Save";
  uploadSamplesButton : string = "Upload";
  updateSamplesButton : string = "Save";

  isCreatingSamples : boolean = false;
  isUpdatingSamples : boolean = false;
  isDeletingSamples : boolean = false;

  private destroy$ = new Subject<void>();

  constructor(private route: ActivatedRoute, private reviewsService: ReviewsService, private configService: ConfigService,
    private apiRevisionsService: APIRevisionsService, private samplesRevisionService: SamplesRevisionService,
    private router: Router, private userProfileService: UserProfileService, private changeDetectorRef: ChangeDetectorRef,
    private messageService: MessageService, private commentsService: CommentsService, private renderer: Renderer2, private el: ElementRef,
    private injector: Injector, private viewContainerRef: ViewContainerRef, private titleService: Title) {}

  ngOnInit() {
    if (this.embedded) {
      this.reviewId = this.embeddedReviewId;
      this.review = this.embeddedReview;
      this.userProfile = this.embeddedUserProfile;
    } else {
      this.reviewId = this.route.snapshot.paramMap.get(REVIEW_ID_ROUTE_PARAM);
      this.userProfileService.getUserProfile().subscribe(
        (userProfile : any) => {
          this.userProfile = userProfile;
      });
      this.loadReview(this.reviewId!);
    }

    this.createSideMenu();
    this.loadLatestAPIRevision(this.reviewId!);

    if (this.embedded) {
      // In embedded mode, load the list once on init and only react to
      // query-param changes that target the samples view (e.g. selecting a sample).
      this.loadSamplesRevisions(0, this.samplesRevisionsPageSize);
      this.route.queryParams.pipe(takeUntil(this.destroy$)).subscribe(params => {
        if (params['view'] !== 'samples') return;
        const navigationState = this.router.currentNavigation()?.extras.state;
        if (!navigationState || !navigationState['skipStateUpdate']) {
          this.updateStateBasedOnQueryParams(params);
        }
      });
    } else {
      this.route.queryParams.pipe(takeUntil(this.destroy$)).subscribe(params => {
        const navigationState = this.router.currentNavigation()?.extras.state;
        if (!navigationState || !navigationState['skipStateUpdate']) {
          this.updateStateBasedOnQueryParams(params);
        }
      });
    }
  }

  ngAfterViewChecked(): void {
    if (!this.samplesContent || !this.commentsLoaded || this.commentableRegionsAdded) {
      return;
    }
    setTimeout(() => {
      if (this.samplesContent && this.commentsLoaded && !this.commentableRegionsAdded) {
        this.addCommentableRegions();
        this.commentableRegionsAdded = true;
      }
    });
  }

  createSideMenu() {
    this.sideMenu = [];
  }

  navigateToConversations() {
    const queryParams: any = {};
    const revisionId = this.activeApiRevisionId ?? this.latestApiRevision?.id;
    if (revisionId) {
      queryParams['activeApiRevisionId'] = revisionId;
    }
    queryParams['view'] = 'conversations';
    this.router.navigate(['/review', this.reviewId], { queryParams: queryParams });
  }

  navigateToRevisions() {
    const queryParams: any = {};
    const revisionId = this.activeApiRevisionId ?? this.latestApiRevision?.id;
    if (revisionId) {
      queryParams['activeApiRevisionId'] = revisionId;
    }
    queryParams['view'] = 'revisions';
    this.router.navigate(['/review', this.reviewId], { queryParams: queryParams });
  }

  navigateToReview() {
    const queryParams: any = {};
    const revisionId = this.activeApiRevisionId ?? this.latestApiRevision?.id;
    if (revisionId) {
      queryParams['activeApiRevisionId'] = revisionId;
    }
    if (this.diffApiRevisionId) {
      queryParams['diffApiRevisionId'] = this.diffApiRevisionId;
    }
    this.router.navigate(['/review', this.reviewId], { queryParams: queryParams });
  }

  selectSample(sample: SamplesRevision) {
    const queryParams: any = { activeSamplesRevisionId: sample.id };
    if (this.activeApiRevisionId) queryParams['activeApiRevisionId'] = this.activeApiRevisionId;
    if (this.diffApiRevisionId) queryParams['diffApiRevisionId'] = this.diffApiRevisionId;
    this.navigateSamples(queryParams);
  }

  backToList() {
    const queryParams: any = { activeSamplesRevisionId: null };
    if (this.activeApiRevisionId) queryParams['activeApiRevisionId'] = this.activeApiRevisionId;
    if (this.diffApiRevisionId) queryParams['diffApiRevisionId'] = this.diffApiRevisionId;
    this.navigateSamples(queryParams);
  }

  navigateToSamplesList() {
    this.backToList();
  }

  confirmDeleteSample(sample: SamplesRevision, event: Event) {
    event.stopPropagation();
    this.sampleToDelete = sample;
    this.showSamplesDeleteModal = true;
  }

  private navigateSamples(queryParams: any) {
    if (this.embedded) {
      const currentParams = getQueryParams(this.route);
      this.router.navigate([], {
        queryParams: { ...currentParams, ...queryParams, view: 'samples' },
        state: { skipStateUpdate: true }
      });
      const merged = { ...currentParams, ...queryParams, view: 'samples' };
      this.updateStateBasedOnQueryParams(merged);
    } else {
      this.router.navigate(['/samples', this.reviewId], { queryParams });
    }
  }

  updateStateBasedOnQueryParams(params: Params) {
    this.activeSamplesRevisionId = params[ACTIVE_SAMPLES_REVISION_ID_QUERY_PARAM];
    this.activeApiRevisionId = params[ACTIVE_API_REVISION_ID_QUERY_PARAM];
    this.diffApiRevisionId = params[DIFF_API_REVISION_ID_QUERY_PARAM];

    if (this.activeSamplesRevisionId) {
      this.activeView = 'detail';
      this.isLoading = true;
      this.loadFailed = false;
      this.commentableRegionsAdded = false;
      this.commentsLoaded = false;
      this.samplesContent = undefined;
      this.changeDetectorRef.detectChanges();
      this.loadComments();
    } else {
      this.activeView = 'list';
      this.isLoading = true;
    }
    this.loadSamplesRevisions(0, this.samplesRevisionsPageSize);
  }

  loadReview(reviewId: string) {
    this.reviewsService.getReview(reviewId)
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: (review: Review) => {
          this.review = review;
          this.updatePageTitle();
        }
    });
  }

  loadComments() {
    this.commentsService.getComments(this.reviewId!, CommentType.SampleRevision)
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: (comments: CommentItemModel[]) => {
          this.comments = comments;
          const groupedComments = comments.reduce((acc, comment) => {
            if (!acc[comment.elementId]) {
              acc[comment.elementId] = [comment];
            } else {
              acc[comment.elementId].push(comment);
            }
            return acc;
          }, {} as { [key: string]: CommentItemModel[] });

          Object.keys(groupedComments).map((key) => {
            const groupOfComments = groupedComments[key];
            const commentThread = new CodePanelRowData();
            commentThread.type = CodePanelRowDatatype.CommentThread;
            commentThread.comments = groupOfComments;
            commentThread.isResolvedCommentThread = groupOfComments.some(x => x.isResolved);
            this.commentThreads.set(key, commentThread);
          });
          this.commentsLoaded = true;
        },
    });
  }

  loadLatestAPIRevision(reviewId: string) {
    this.apiRevisionsService.getLatestAPIRevision(reviewId)
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: (apiRevision: APIRevision) => {
          this.latestApiRevision = apiRevision;
        }
    });
  }

  loadSamplesRevisions(noOfItemsRead : number, pageSize: number) {
    this.samplesRevisionService.getSamplesRevisions(noOfItemsRead, pageSize, this.reviewId!)
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: (paginatedResult: PaginatedResult<SamplesRevision[]>) => {
          this.samplesRevisions = paginatedResult.result!;
          this.samplesRevisionTotalCount = paginatedResult.pagination?.totalCount ?? this.samplesRevisions.length;
          this.activeSamplesRevision = this.samplesRevisions.filter(x => x.id === this.activeSamplesRevisionId)[0];
          this.loadActiveSampleRevisionData();
        }
    });
  }

  loadActiveSampleRevisionData() {
    if (this.activeView === 'list') {
      this.isLoading = false;
      return;
    }

    if (this.samplesRevisions && this.samplesRevisions.length > 0 && this.activeSamplesRevisionId) {
      this.activeSamplesRevision = this.samplesRevisions.filter(x => x.id === this.activeSamplesRevisionId)[0];
      if (this.activeSamplesRevision) {
        this.loadSamplesContent(this.reviewId!, this.activeSamplesRevision.id);
        return;
      }
    }

    // Sample not found or no ID - fall back to list
    this.activeView = 'list';
    this.isLoading = false;
  }

  loadSamplesContent(reviewId: string, activeSamplesRevisionId: string | null = null) {
    this.samplesRevisionService.getSamplesContent(reviewId, activeSamplesRevisionId)
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: (content: string) => {
          this.samplesContent = content;
          this.isLoading = false;
          this.changeDetectorRef.detectChanges();
        },
        error: (error: any) => {
          this.loadFailed = true;
        }
      });
  }

  onSamplesUploadFileSelect(event: FileSelectEvent) {
    this.samplesUploadFile = event.currentFiles[0];
  }

  onFileDrop(event: DragEvent) {
    const file = event.dataTransfer?.files[0];
    if (file && file.name.toLowerCase().endsWith('.md')) {
      this.samplesUploadFile = file;
    } else if (file) {
      this.messageService.add({
        severity: 'error',
        icon: 'bi bi-exclamation-triangle',
        summary: 'Invalid file type',
        detail: 'Only Markdown (.md) files can be uploaded as samples.',
        key: 'bc',
        life: 3000
      });
    }
  }

  onFileInputChange(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (file && file.name.toLowerCase().endsWith('.md')) {
      this.samplesUploadFile = file;
    } else if (file) {
      this.samplesUploadFile = undefined;
      this.messageService.add({
        severity: 'error',
        icon: 'bi bi-exclamation-triangle',
        summary: 'Invalid file type',
        detail: 'Only Markdown (.md) files can be uploaded as samples.',
        key: 'bc',
        life: 3000
      });
    }
  }

  openLatestAPIReivisonForReview() {
    if (this.activeApiRevisionId) {
      const queryParams: any = { activeApiRevisionId: this.activeApiRevisionId };
      if (this.diffApiRevisionId) {
        queryParams['diffApiRevisionId'] = this.diffApiRevisionId;
      }
      this.router.navigate(['/review', this.reviewId], { queryParams: queryParams });
    } else {
      this.apiRevisionsService.openAPIRevisionPage(this.latestApiRevision!, this.route);
    }
  }

  showAddSamplesPanel() {
    this.samplesUpdateSidePanel = !this.samplesUpdateSidePanel;
    this.samplesUpdateState = 'add';
    this.addEditSamplesTitle = "";
    this.addEditSamplesContent = this.SAMPLES_CONTENT_PLACEHOLDER;
  }

  showEditSamplesPanel() {
    this.samplesUpdateSidePanel = !this.samplesUpdateSidePanel;
    this.samplesUpdateState = 'edit';
    this.addEditSamplesTitle = this.activeSamplesRevision!.title;
    this.addEditSamplesContent = this.samplesContent!;
  }

  createUsageSample() {
    this.isCreatingSamples = true;
    const formData: FormData = new FormData();
    if (this.samplesUploadFile) {
      formData.append('file', this.samplesUploadFile!, this.samplesUploadFile!.name);
      this.uploadSamplesButton = "Uploading Usage Sample...";
    }

    const samplesContent = this.getAddEditSamplesContent();
    if (samplesContent) {
      formData.append('content', samplesContent);
      this.createSamplesButton = "Creating Usage Sample...";
    }
    formData.append('title', this.addEditSamplesTitle);

    this.samplesRevisionService.createUsageSample(this.reviewId!, formData)
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: (samplesRevision: SamplesRevision) => {
          this.samplesUpdateSidePanel = false;
          this.isCreatingSamples = false;
          this.createSamplesButton = "Save";
          this.uploadSamplesButton = "Upload";

          const queryParams: any = { activeSamplesRevisionId: samplesRevision.id };
          if (this.activeApiRevisionId) queryParams['activeApiRevisionId'] = this.activeApiRevisionId;
          if (this.diffApiRevisionId) queryParams['diffApiRevisionId'] = this.diffApiRevisionId;

          this.navigateSamples(queryParams);
        },
        error: (error: any) => {
          this.isCreatingSamples = false;
          this.createSamplesButton = "Save";
          this.uploadSamplesButton = "Upload";
          this.messageService.add({ severity: 'error', icon: 'bi bi-exclamation-triangle', summary: 'Samples Failure', detail: 'Failed to create new Usage Sample', key: 'bc', life: 3000 });
        }
      });
  }

  updateUsageSample() {
    this.isUpdatingSamples = true;
    this.updateSamplesButton = "Updating Usage Sample...";
    const formData: FormData = new FormData();

    formData.append('content', this.getAddEditSamplesContent());
    formData.append('title', this.addEditSamplesTitle);

    this.samplesRevisionService.updateUsageSample(this.reviewId!, this.activeSamplesRevisionId!, formData)
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: () => {
          this.samplesUpdateSidePanel = false;
          this.isUpdatingSamples = false;
          this.updateSamplesButton = "Save";
          // Reload the detail view content
          this.commentableRegionsAdded = false;
          this.commentsLoaded = false;
          this.samplesContent = undefined;
          this.changeDetectorRef.detectChanges();
          this.loadSamplesContent(this.reviewId!, this.activeSamplesRevisionId!);
          this.loadComments();
        },
        error: (error: any) => {
          this.isUpdatingSamples = false;
          this.updateSamplesButton = "Save";
          this.messageService.add({ severity: 'error', icon: 'bi bi-exclamation-triangle', summary: 'Samples Failure', detail: 'Failed to update Usage Sample', key: 'bc', life: 3000 });
        }
      });
  }

  deleteUsageSample() {
    const revisionId = this.sampleToDelete?.id ?? this.activeSamplesRevisionId!;
    this.isDeletingSamples = true;
    this.deleteSamplesButton = "Deleting Usage Sample...";
    this.samplesRevisionService.deleteSampleRevisions(this.reviewId!, [revisionId])
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: () => {
          this.showSamplesDeleteModal = false;
          this.isDeletingSamples = false;
          this.deleteSamplesButton = "Delete";

          if (this.activeView === 'detail') {
            this.backToList();
          } else {
            this.samplesRevisions = this.samplesRevisions.filter(s => s.id !== revisionId);
            this.samplesRevisionTotalCount = Math.max(0, this.samplesRevisionTotalCount - 1);
            this.sampleToDelete = null;
          }
        },
        error: (error: any) => {
          this.showSamplesDeleteModal = false;
          this.isDeletingSamples = false;
          this.deleteSamplesButton = "Delete";
          this.sampleToDelete = null;
          this.messageService.add({ severity: 'error', icon: 'bi bi-exclamation-triangle', summary: 'Samples Failure', detail: 'Failed to delete Usage Sample', key: 'bc', life: 3000 });
        }});
  }

  getAddEditSamplesContent() {
    const placeholderRegex = new RegExp(this.SAMPLES_CONTENT_PLACEHOLDER, 'g');
    return this.addEditSamplesContent.replace(placeholderRegex, "");
  }

  handleContentValueChange(content: string) {
    this.addEditSamplesContent = content;
  }

  addCommentableRegions(): void {
    const sampleSection = this.el.nativeElement.querySelectorAll('pre > code .line-actions');

    sampleSection.forEach((element: HTMLElement) => {
      const title = element.getAttribute('title');
      if (title && this.commentThreads.has(title)) {
        const commentIcon = element.querySelector('.toggle-user-comments-btn');
        this.showCommentThread(commentIcon as HTMLElement);
      }
    });

    this.el.nativeElement.querySelectorAll('pre > code').forEach((element: HTMLElement) => {
      this.renderer.listen(element, 'click', (event: MouseEvent) => {
        const target = event.target as HTMLElement;
        if (target && target.classList.contains('toggle-user-comments-btn') && !target.classList.contains('hide') && !target.classList.contains('show')) {
          target.classList.add('temp-show');
          const title = target.closest('.line-actions')?.getAttribute('title');
          const commentThreadContainer = this.createCommentThreadContainer(title!);
          this.insertCommentThread(commentThreadContainer, target);
        }
      });
    });

    this.renderer.listen('document', 'mousemove', (event: MouseEvent) => {
      const mouseY = event.clientY;

      sampleSection.forEach((element: HTMLElement) => {
        const rect = element.getBoundingClientRect();
        const commentIcon = element.querySelector('.icon.bi.bi-chat-right-text');

        if (mouseY >= rect.top && mouseY <= rect.bottom) {
          if (commentIcon && commentIcon.classList.contains('toggle-user-comments-btn') && commentIcon.classList.contains('can-show')) {
            this.renderer.addClass(commentIcon, 'temp-show');
          }
        } else {
          if (commentIcon && commentIcon.classList.contains('toggle-user-comments-btn') && commentIcon.classList.contains('can-show')) {
            this.renderer.removeClass(commentIcon, 'temp-show');
          }
        }
      });
    });
  }

  showCommentThread(target: HTMLElement): void {
    const title = target.closest('.line-actions')?.getAttribute('title');
    const commentThread = this.commentThreads.get(title!);

    const commentThreadContainer = this.createCommentThreadContainer(title!, commentThread);
    this.insertCommentThread(commentThreadContainer, target);
  }

  hideCommentThread(): void {
    const existingContainer = this.el.nativeElement.querySelector('.user-comment-thread');
    if (existingContainer) {
      this.renderer.removeChild(existingContainer.parentNode, existingContainer);
    }
  }

  private createCommentThreadContainer(title: string, commentThread: CodePanelRowData | undefined = undefined): HTMLElement {
    const commentThreadContainer = this.renderer.createElement('div');
    this.renderer.setAttribute(commentThreadContainer, 'title', title);
    this.renderer.addClass(commentThreadContainer, 'user-comment-thread');
    this.renderer.addClass(commentThreadContainer, 'border-top');
    this.renderer.addClass(commentThreadContainer, 'border-bottom');
    this.renderer.addClass(commentThreadContainer, 'ps-4');
    this.renderer.addClass(commentThreadContainer, 'py-2');

    const componentRef = this.viewContainerRef.createComponent(CommentThreadComponent, { injector: this.injector });

    if (commentThread) {
      componentRef.instance.codePanelRowData = commentThread;
    } else {
      commentThread = new CodePanelRowData();
      commentThread.type = CodePanelRowDatatype.CommentThread;
      commentThread.showReplyTextBox = true;
      commentThread.comments = [];
      componentRef.instance.codePanelRowData = commentThread;
    }

    componentRef.instance.cancelCommentActionEmitter.subscribe((commentUpdates: any) => {
      if (commentUpdates.title && !this.commentThreads.has(commentUpdates.title)) {
        if (commentThread!.comments.length === 0) {
          this.removeCommentThread(commentUpdates.title);
        }
      }
    });

    componentRef.instance.saveCommentActionEmitter.subscribe((commentUpdates: any) => {
      if (commentUpdates.commentId) {
        this.commentsService.updateComment(this.reviewId!, commentUpdates.commentId, commentUpdates.commentText!)
          .pipe(take(1)).subscribe({
            next: () => {
              commentThread!.comments!.filter(x => x.id === commentUpdates.commentId)[0].commentText = commentUpdates.commentText;
            }
          });
      }
      else {
        this.commentsService.createComment(this.reviewId!, this.activeSamplesRevisionId!, commentUpdates.elementId!, commentUpdates.commentText!, CommentType.SampleRevision, commentUpdates.allowAnyOneToResolve !== undefined ? !commentUpdates.allowAnyOneToResolve : false, commentUpdates.severity, commentUpdates.threadId)
          .pipe(take(1)).subscribe({
              next: (response: CommentItemModel) => {
                commentThread!.comments = [...commentThread!.comments!, response];
              }
            }
          );
      }
    });

    componentRef.instance.deleteCommentActionEmitter.subscribe((commentUpdates: any) => {
      this.commentsService.deleteComment(this.reviewId!, commentUpdates.commentId!).pipe(take(1)).subscribe({
        next: () => {
          commentThread!.comments = commentThread!.comments!.filter(x => x.id !== commentUpdates.commentId);
          if (commentThread!.comments.length === 0) {
            this.removeCommentThread(commentUpdates.title);
          }
        }
      });
    });

    componentRef.instance.commentResolutionActionEmitter.subscribe((commentUpdates: any) => {
      commentUpdates.reviewId = this.reviewId!;
      if (commentUpdates.commentThreadUpdateAction === CommentThreadUpdateAction.CommentResolved) {
        this.commentsService.resolveComments(this.reviewId!, commentUpdates.elementId!).pipe(take(1)).subscribe({
          next: () => {
            const ct = this.applyCommentResolutionUpdate(commentThread!, commentUpdates);
            componentRef.instance.ngOnChanges({
              codePanelRowData: new SimpleChange(null, ct, false)
            });
          }
        });
      }
      if (commentUpdates.commentThreadUpdateAction === CommentThreadUpdateAction.CommentUnResolved) {
        this.commentsService.unresolveComments(this.reviewId!, commentUpdates.elementId!).pipe(take(1)).subscribe({
          next: () => {
            const ct = this.applyCommentResolutionUpdate(commentThread!, commentUpdates);
            componentRef.instance.ngOnChanges({
              codePanelRowData: new SimpleChange(null, ct, false)
            });
          }
        });
      }
    });

    componentRef.instance.commentUpvoteActionEmitter.subscribe((commentUpdates: any) => {
      this.commentsService.toggleCommentUpVote(this.reviewId!, commentUpdates.commentId!).pipe(take(1)).subscribe({
        next: () => {
          const comment = commentThread!.comments!.filter(x => x.id == commentUpdates.commentId)[0];
          if (comment) {
            if (comment.upvotes.includes(this.userProfile?.userName!)) {
              commentThread!.comments!.filter(x => x.id == commentUpdates.commentId)[0].upvotes.splice(comment.upvotes.indexOf(this.userProfile?.userName!), 1);
            } else {
              commentThread!.comments!.filter(x => x.id == commentUpdates.commentId)[0].upvotes.push(this.userProfile?.userName!);
            }
          }
        }
      });
    });

    componentRef.instance.batchResolutionActionEmitter.subscribe(() => {
      this.loadComments();
    });

    componentRef.instance.instanceLocation = 'samples';
    componentRef.instance.userProfile = this.userProfile;
    this.renderer.appendChild(commentThreadContainer, componentRef.location.nativeElement);
    return commentThreadContainer;
  }

  private applyCommentResolutionUpdate(commentThread: CodePanelRowData, commentUpdates: CommentUpdatesDto) : CodePanelRowData {
    commentThread.isResolvedCommentThread = (commentUpdates.commentThreadUpdateAction === CommentThreadUpdateAction.CommentResolved)? true : false;
    commentThread.commentThreadIsResolvedBy = commentUpdates.resolvedBy!;
    return commentThread;
  }

  private removeCommentThread(title: string): void {
    this.el.nativeElement.querySelector(`.user-comment-thread[title="${title}"]`).remove();
    const targetCommentIcon = this.el.nativeElement.querySelectorAll(`.line-actions[title="${title}"] > .toggle-user-comments-btn`);
    targetCommentIcon.forEach((targetCommentIcon: HTMLElement) => {
      if (targetCommentIcon.classList.contains('show') && targetCommentIcon.classList.contains('temp-show')) {
        targetCommentIcon.classList.remove('show', 'temp-show');
        targetCommentIcon.classList.add('can-show');
      }
    });
  }

  private insertCommentThread(commentThreadContainer: HTMLElement, target: HTMLElement): void {
    const closestLineActions = target.closest('.line-actions');
    if (closestLineActions) {
      let nextLineActionsSibling = closestLineActions.nextElementSibling;
      while (nextLineActionsSibling && !nextLineActionsSibling.classList.contains('line-actions')) {
        nextLineActionsSibling = nextLineActionsSibling.nextElementSibling;
      }
      if (nextLineActionsSibling) {
        this.renderer.insertBefore(closestLineActions.parentNode, commentThreadContainer, nextLineActionsSibling);
      } else {
        this.renderer.appendChild(target.closest('code'), commentThreadContainer);
      }
      target.classList.remove('hide', 'can-show')
      target.classList.add('show');
    }
  }

  updatePageTitle() {
    if (this.review?.packageName) {
      this.titleService.setTitle(this.review.packageName);
    } else {
      this.titleService.setTitle('APIView');
    }
  }
}
