import { ChangeDetectionStrategy, ChangeDetectorRef, Component, EventEmitter, Input, OnChanges, OnDestroy, Output, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TimeagoModule } from 'ngx-timeago';
import { TimelineModule } from 'primeng/timeline';
import { CommentThreadComponent } from '../shared/comment-thread/comment-thread.component';
import { LastUpdatedOnPipe } from 'src/app/_pipes/last-updated-on.pipe';
import { CodePanelRowData, CodePanelRowDatatype } from 'src/app/_models/codePanelModels';
import { CommentItemModel, CommentType, CommentSource, CommentSeverity } from 'src/app/_models/commentItemModel';
import { APIRevision } from 'src/app/_models/revision';
import { getTypeClass } from 'src/app/_helpers/common-helpers';
import { getVisibleComments } from 'src/app/_helpers/comment-visibility.helper';
import { CommentSeverityHelper } from 'src/app/_helpers/comment-severity.helper';
import { CommentsService } from 'src/app/_services/comments/comments.service';
import { Subject, take, takeUntil } from 'rxjs';
import { Review } from 'src/app/_models/review';
import { UserProfile } from 'src/app/_models/userProfile';
import { CommentThreadUpdateAction, CommentUpdatesDto } from 'src/app/_dtos/commentThreadUpdateDto';
import { SignalRService } from 'src/app/_services/signal-r/signal-r.service';

const UNKNOWN_SEVERITY_KEY = 'unknown';

@Component({
    selector: 'app-conversations',
    templateUrl: './conversations.component.html',
    styleUrls: ['./conversations.component.scss'],
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        TimeagoModule,
        TimelineModule,
        CommentThreadComponent,
        LastUpdatedOnPipe
    ],
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class ConversationsComponent implements OnChanges, OnDestroy {
  @Input() apiRevisions: APIRevision[] = [];
  @Input() activeApiRevisionId: string | null = null;
  @Input() comments: CommentItemModel[] = [];
  @Input() review : Review | undefined = undefined;
  @Input() userProfile : UserProfile | undefined;
  @Input() allCodePanelRowData: CodePanelRowData[] = [];

  @Output() scrollToNodeEmitter : EventEmitter<string> = new EventEmitter<string>();
  @Output() numberOfActiveThreadsEmitter : EventEmitter<number> = new EventEmitter<number>();
  @Output() dismissSidebarAndNavigateEmitter : EventEmitter<{revisionId: string, elementId: string}> = new EventEmitter<{revisionId: string, elementId: string}>();

  private readonly MAX_DIAGNOSTICS_DISPLAY = 250;

  commentThreads: Map<string, CodePanelRowData[]> = new Map<string, CodePanelRowData[]>();
  numberOfActiveThreads: number = 0;
  // Flag to indicate if diagnostics were truncated due to limit
  diagnosticsTruncated: boolean = false;
  totalDiagnosticsInRevision: number = 0;
  hiddenUnresolvedDiagnosticsCount: number = 0;
  hiddenResolvedDiagnosticsCount: number = 0;

  apiRevisionsWithComments: APIRevision[] = [];

  apiRevisionsLoaded = false;
  commentsLoaded = false;
  isLoading: boolean = true;

  destroy$ = new Subject<void>();

  // --- Filter state ---
  filterStatus: 'all' | 'active' | 'resolved' = 'active';
  // Use string keys matching the JSON-serialized enum values from the C# backend
  filterSeverities: Set<string> = new Set();
  filterKinds: Set<'human' | 'ai' | 'diagnostic'> = new Set();

  // Severity options for template iteration
  readonly severityOptions = [
    { key: 'question', label: 'Question', icon: 'bi-question-circle' },
    { key: 'suggestion', label: 'Suggestion', icon: 'bi-lightbulb' },
    { key: 'shouldfix', label: 'Should Fix', icon: 'bi-exclamation-triangle' },
    { key: 'mustfix', label: 'Must Fix', icon: 'bi-exclamation-octagon-fill' },
    { key: UNKNOWN_SEVERITY_KEY, label: 'Unknown', icon: 'bi-dash-circle' },
  ];

  // Filtered view
  filteredCommentThreads: Map<string, CodePanelRowData[]> = new Map();
  filteredApiRevisionsWithComments: APIRevision[] = [];
  filteredThreadCount: number = 0;
  totalThreadCount: number = 0;
  showUnknownSeverityFilter: boolean = false;
  hasAnyUnknownThreads: boolean = false;

  // Keep filter behavior aligned with the displayed severity badge/label in CommentThread,
  // which is derived from codePanelRowData.comments[0].severity.
  private getDisplayedThreadSeverityKey(thread: CodePanelRowData): string {
    const firstComment = thread.comments?.[0];
    if (!firstComment) return UNKNOWN_SEVERITY_KEY;
    return CommentSeverityHelper.normalizeSeverity(firstComment.severity) ?? UNKNOWN_SEVERITY_KEY;
  }

  private threadMatchesStatusAndKindFilters(thread: CodePanelRowData): boolean {
    const firstComment = thread.comments?.[0];
    if (!firstComment) return false;

    if (this.filterStatus === 'active' && thread.isResolvedCommentThread) return false;
    if (this.filterStatus === 'resolved' && !thread.isResolvedCommentThread) return false;

    if (this.filterKinds.size > 0) {
      const kind = this.getThreadKind(firstComment);
      if (!this.filterKinds.has(kind)) return false;
    }

    return true;
  }

  constructor(private commentsService: CommentsService, private signalRService: SignalRService, private changeDetectorRef: ChangeDetectorRef) { }

  ngOnInit() {
    this.handleRealTimeCommentUpdates();

    this.commentsService.severityChanged$.pipe(takeUntil(this.destroy$)).subscribe(({ commentId, newSeverity }) => {
      const comment = this.comments.find(c => c.id === commentId);
      if (comment) {
        comment.severity = newSeverity;
        this.changeDetectorRef.markForCheck();
      }
    });
  }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['apiRevisions']) {
      this.apiRevisionsLoaded = true;
    }

    if (changes['comments']) {
      this.commentsLoaded = true;
    }

    // Recalculate when active revision changes (diagnostic comments are filtered by revision)
    if (changes['activeApiRevisionId'] && this.apiRevisionsLoaded && this.commentsLoaded) {
      this.createCommentThreads();
      return;
    }

    if (this.apiRevisionsLoaded && this.commentsLoaded) {
      this.createCommentThreads();
    }
  }

  createCommentThreads() {
    if (this.apiRevisions.length > 0 && this.comments.length > 0) {
      this.commentThreads = new Map<string, CodePanelRowData[]>();
      this.numberOfActiveThreads = 0;
      this.diagnosticsTruncated = false;
      this.hasAnyUnknownThreads = false;
      this.showUnknownSeverityFilter = false;
      this.hiddenUnresolvedDiagnosticsCount = 0;
      this.hiddenResolvedDiagnosticsCount = 0;
      this.totalDiagnosticsInRevision = 0;

      // Use shared visibility logic — single source of truth for which comments are relevant
      const { allVisibleComments, diagnosticCommentsForRevision } = getVisibleComments(this.comments, this.activeApiRevisionId);

      this.totalDiagnosticsInRevision = diagnosticCommentsForRevision.length;

      // Sort priority: 1) MustFix unresolved, 2) other unresolved, 3) resolved (secondary: severity desc)
      const getTier = (c: CommentItemModel) => {
        if ((CommentSeverityHelper.getSeverityEnumValue(c.severity) ?? -1) >= CommentSeverity.MustFix && !c.isResolved) return 0;
        if (!c.isResolved) return 1;
        return 2;
      };
      const sortedDiagnostics = [...diagnosticCommentsForRevision].sort((a, b) => {
        const tierDiff = getTier(a) - getTier(b);
        if (tierDiff !== 0) return tierDiff;
        return (CommentSeverityHelper.getSeverityEnumValue(b.severity) ?? -1) - (CommentSeverityHelper.getSeverityEnumValue(a.severity) ?? -1);
      });

      // Always show all MustFix unresolved; fill remaining slots up to MAX_DIAGNOSTICS_DISPLAY
      const mustFixUnresolvedCount = diagnosticCommentsForRevision.filter(
        c => (CommentSeverityHelper.getSeverityEnumValue(c.severity) ?? -1) >= CommentSeverity.MustFix && !c.isResolved
      ).length;
      const displayLimit = Math.max(this.MAX_DIAGNOSTICS_DISPLAY, mustFixUnresolvedCount);

      const limitedDiagnostics = sortedDiagnostics.slice(0, displayLimit);
      const hiddenDiagnostics = sortedDiagnostics.slice(displayLimit);
      this.hiddenUnresolvedDiagnosticsCount = hiddenDiagnostics.filter(c => !c.isResolved).length;
      this.hiddenResolvedDiagnosticsCount = hiddenDiagnostics.filter(c => c.isResolved).length;
      this.diagnosticsTruncated = hiddenDiagnostics.length > 0;
      const filteredComments = [
        ...allVisibleComments.filter(c => c.commentSource !== CommentSource.Diagnostic),
        ...limitedDiagnostics
      ];

      // Count ALL visible unresolved threads for the badge — this must match what
      // the quality score counts so the numbers stay consistent across the UI.
      const allThreadGroups = allVisibleComments.reduce((acc: { [key: string]: CommentItemModel[] }, comment) => {
        const threadKey = comment.threadId || comment.elementId;
        if (!acc[threadKey]) {
          acc[threadKey] = [];
        }
        acc[threadKey].push(comment);
        return acc;
      }, {});

      for (const threadId in allThreadGroups) {
        if (allThreadGroups.hasOwnProperty(threadId)) {
          const comments = allThreadGroups[threadId];
          const isResolved = comments.some(c => c.isResolved);
          if (!isResolved) {
            this.numberOfActiveThreads++;
          }
        }
      }

      // Build the display-only thread groups (capped diagnostics, mapped to loaded revisions).
      // This does NOT affect the badge count above.
      const threadGroups = filteredComments.reduce((acc: { [key: string]: CommentItemModel[] }, comment) => {
        const threadKey = comment.threadId || comment.elementId;
        if (!acc[threadKey]) {
          acc[threadKey] = [];
        }
        acc[threadKey].push(comment);
        return acc;
      }, {});

      const apiRevisionInOrder = this.apiRevisions.sort((a, b) => (new Date(b.createdOn) as any) - (new Date(a.createdOn) as any));

      const apiRevisionPositionMap = new Map<string, number>();
      apiRevisionInOrder.forEach((rev, index) => {
        apiRevisionPositionMap.set(rev.id, index);
      });

      for (const threadId in threadGroups) {
        if (threadGroups.hasOwnProperty(threadId)) {
          const comments = threadGroups[threadId];
          const apiRevisionIds = comments.map(c => c.apiRevisionId);

          let apiRevisionPostion = Number.MAX_SAFE_INTEGER;

          for (const apiRevisionId of apiRevisionIds) {
            const position = apiRevisionPositionMap.get(apiRevisionId);
            if (position !== undefined && position < apiRevisionPostion) {
              apiRevisionPostion = position;
            }
          }

          // If the thread's apiRevisionId doesn't match any loaded revision,
          // fall back to the active revision so it still appears in the panel.
          if (apiRevisionPostion === Number.MAX_SAFE_INTEGER && this.activeApiRevisionId) {
            const activePosition = apiRevisionPositionMap.get(this.activeApiRevisionId);
            if (activePosition !== undefined) {
              apiRevisionPostion = activePosition;
            }
          }

          if (apiRevisionPostion >= 0 && apiRevisionPostion < apiRevisionInOrder.length) {
            const apiRevisionIdForThread = apiRevisionInOrder[apiRevisionPostion].id;
            const codePanelRowData = new CodePanelRowData();
            codePanelRowData.type = CodePanelRowDatatype.CommentThread;
            codePanelRowData.comments = comments;
            codePanelRowData.threadId = threadId;
            codePanelRowData.isResolvedCommentThread = comments.some(c => c.isResolved);

            if (!this.hasAnyUnknownThreads && this.getDisplayedThreadSeverityKey(codePanelRowData) === UNKNOWN_SEVERITY_KEY) {
              this.hasAnyUnknownThreads = true;
            }

            if (this.commentThreads.has(apiRevisionIdForThread)) {
              this.commentThreads.get(apiRevisionIdForThread)?.push(codePanelRowData);
            }
            else {
              this.commentThreads.set(apiRevisionIdForThread, [codePanelRowData]);
            }
          }
        }
      }

      this.numberOfActiveThreadsEmitter.emit(this.numberOfActiveThreads);
      this.apiRevisionsWithComments = this.apiRevisions.filter(apiRevision => this.commentThreads.has(apiRevision.id));

      this.updateUnknownSeverityFilterVisibility();
      this.applyFilters();
      this.isLoading = false;
      this.changeDetectorRef.markForCheck();
    }
    else if (this.apiRevisions.length > 0 && this.comments.length === 0) {
      this.apiRevisionsWithComments = [];
      this.filteredApiRevisionsWithComments = [];
      this.filteredCommentThreads = new Map();
      this.filteredThreadCount = 0;
      this.totalThreadCount = 0;
      this.hasAnyUnknownThreads = false;
      this.showUnknownSeverityFilter = false;
      this.filterSeverities.delete(UNKNOWN_SEVERITY_KEY);
      this.numberOfActiveThreads = 0;
      this.numberOfActiveThreadsEmitter.emit(this.numberOfActiveThreads);
      setTimeout(() => {
        this.isLoading = false;
        this.changeDetectorRef.markForCheck();
      }, 1000);
    }
  }

  getAPIRevisionWithComments() {
    return this.apiRevisionsWithComments;
  }

  getAPIRevisionTypeClass(apiRevision: APIRevision) {
    return getTypeClass(apiRevision.apiRevisionType);
  }

  navigateToElement(revisionId: string, elementId: string) {
    if (this.activeApiRevisionId && this.activeApiRevisionId === revisionId) {
      this.scrollToNodeEmitter.emit(elementId);
    } else {
      this.dismissSidebarAndNavigateEmitter.emit({
        revisionId: revisionId,
        elementId: elementId
      });
    }
  }

  handleRealTimeCommentUpdates() {
    this.signalRService.onCommentUpdates().pipe(takeUntil(this.destroy$)).subscribe({
      next: (commentUpdates: CommentUpdatesDto) => {
        if ((commentUpdates.reviewId && commentUpdates.reviewId == this.review?.id) ||
          (commentUpdates.comment && commentUpdates.comment.reviewId == this.review?.id)) {
          switch (commentUpdates.commentThreadUpdateAction) {
            case CommentThreadUpdateAction.CommentCreated:
              this.addCommentToCommentThread(commentUpdates);
              break;
            case CommentThreadUpdateAction.CommentTextUpdate:
              this.updateCommentTextInCommentThread(commentUpdates);
              break;
            case CommentThreadUpdateAction.CommentResolved:
              this.applyCommentResolutionUpdate(commentUpdates);
              break;
            case CommentThreadUpdateAction.CommentUnResolved:
              this.applyCommentResolutionUpdate(commentUpdates);
              break;
            case CommentThreadUpdateAction.CommentUpVoteToggled:
              this.toggleCommentUpVote(commentUpdates);
              break;
            case CommentThreadUpdateAction.CommentDownVoteToggled:
              this.toggleCommentDownVote(commentUpdates);
              break;
            case CommentThreadUpdateAction.CommentDeleted:
              this.deleteCommentFromCommentThread(commentUpdates);
              break;
            case CommentThreadUpdateAction.AutoGeneratedCommentsDeleted:
              this.removeAllAutoGeneratedComments();
              break;
          }
          this.changeDetectorRef.markForCheck();
        }
      }
    });
  }

  trackByThreadId(index: number, commentThread: CodePanelRowData): string {
    return commentThread.threadId || `${index}`;
  }

  handleSaveCommentActionEmitter(commentUpdates: CommentUpdatesDto) {
    commentUpdates.reviewId = this.review?.id!;
    if (commentUpdates.commentId) {
    }
    else {
      const isNewThread = commentUpdates.isReply === false;
      const resolutionLocked = commentUpdates.allowAnyOneToResolve !== undefined ? !commentUpdates.allowAnyOneToResolve : false;
      this.commentsService.createComment(this.review?.id!, commentUpdates.revisionId!, commentUpdates.elementId!, commentUpdates.commentText!, CommentType.APIRevision, resolutionLocked, commentUpdates.severity, commentUpdates.threadId)
        .pipe(take(1)).subscribe({
            next: (response: CommentItemModel) => {
              commentUpdates.comment = response;
              // Ensure threadId is set from response if not already present
              if (!commentUpdates.threadId && response.threadId) {
                commentUpdates.threadId = response.threadId;
              }
              this.addCommentToCommentThread(commentUpdates);
              this.signalRService.pushCommentUpdates(commentUpdates);
              // Only refresh quality score for new threads, not replies
              if (isNewThread) {
                this.commentsService.notifyQualityScoreRefresh();
              }
            }
          }
        );
    }
  }

  handleCommentUpvoteActionEmitter(commentUpdates: CommentUpdatesDto){
    commentUpdates.reviewId = this.review?.id!;
    this.commentsService.toggleCommentUpVote(this.review?.id!, commentUpdates.commentId!).pipe(take(1)).subscribe({
      next: () => {
        this.toggleCommentUpVote(commentUpdates);
        this.signalRService.pushCommentUpdates(commentUpdates);
      }
    });
  }

  handleCommentDownvoteActionEmitter(commentUpdates: CommentUpdatesDto){
    commentUpdates.reviewId = this.review?.id!;
    this.commentsService.toggleCommentDownVote(this.review?.id!, commentUpdates.commentId!).pipe(take(1)).subscribe({
      next: () => {
        this.toggleCommentDownVote(commentUpdates);
        this.signalRService.pushCommentUpdates(commentUpdates);
      }
    });
  }

  handleDeleteCommentActionEmitter(commentUpdates: CommentUpdatesDto) {
    commentUpdates.reviewId = this.review?.id!;
    this.commentsService.deleteComment(this.review?.id!, commentUpdates.commentId!).pipe(take(1)).subscribe({
      next: () => {
        this.deleteCommentFromCommentThread(commentUpdates);
        this.signalRService.pushCommentUpdates(commentUpdates);
        this.commentsService.notifyQualityScoreRefresh();
      }
    });
  }

  handleCommentResolutionActionEmitter(commentUpdates: CommentUpdatesDto) {
    commentUpdates.reviewId = this.review?.id!;
    const hasRealThreadId = commentUpdates.threadId != null &&
      this.comments.some(c => c.threadId === commentUpdates.threadId);
    const threadIdForApi = hasRealThreadId ? commentUpdates.threadId : undefined;

    if (commentUpdates.commentThreadUpdateAction === CommentThreadUpdateAction.CommentResolved) {
      this.commentsService.resolveComments(this.review?.id!, commentUpdates.elementId!, threadIdForApi).pipe(take(1)).subscribe({
        next: () => {
          this.applyCommentResolutionUpdate(commentUpdates);
          this.commentsService.notifyQualityScoreRefresh();
        }
      });
    }
    if (commentUpdates.commentThreadUpdateAction === CommentThreadUpdateAction.CommentUnResolved) {
      this.commentsService.unresolveComments(this.review?.id!, commentUpdates.elementId!, threadIdForApi).pipe(take(1)).subscribe({
        next: () => {
          this.applyCommentResolutionUpdate(commentUpdates);
          this.commentsService.notifyQualityScoreRefresh();
        }
      });
    }
  }

  handleBatchResolutionActionEmitter(commentUpdates: CommentUpdatesDto) {
    commentUpdates.reviewId = this.review?.id!;

    switch (commentUpdates.commentThreadUpdateAction) {
      case CommentThreadUpdateAction.CommentCreated:
        if (commentUpdates.comment) {
          this.addCommentToCommentThread(commentUpdates);
        }
        break;
      case CommentThreadUpdateAction.CommentResolved:
        this.applyCommentResolutionUpdate(commentUpdates);
        break;
    }
  }

  private updateCommentTextInCommentThread(commentUpdates: CommentUpdatesDto) {
    if (this.comments.some(c => c.id === commentUpdates.commentId!)) {
      this.comments.find(c => c.id === commentUpdates.commentId!)!.commentText = commentUpdates.commentText!;
    }
  }

  private addCommentToCommentThread(commentUpdates: CommentUpdatesDto) {
    if (!this.comments.some(c => c.id === commentUpdates.comment!.id)) {
      this.comments.push(commentUpdates.comment!);
      this.createCommentThreads();
    }
  }

  private applyCommentResolutionUpdate(commentUpdates: CommentUpdatesDto) {
    const isResolved = commentUpdates.commentThreadUpdateAction === CommentThreadUpdateAction.CommentResolved;
    this.comments.filter(c => {
      if (commentUpdates.threadId) {
        // Match by threadId normally; in legacy cases where threadId was used as elementId,
        // only match comments without a threadId whose elementId equals the provided elementId.
        return c.threadId === commentUpdates.threadId ||
               (!c.threadId &&
                commentUpdates.elementId === commentUpdates.threadId &&
                c.elementId === commentUpdates.elementId);
      }
      return c.elementId === commentUpdates.elementId;
    }).forEach(c => {
      c.isResolved = isResolved;
    });
    this.createCommentThreads();
  }

  private deleteCommentFromCommentThread(commentUpdates: CommentUpdatesDto) {
    this.comments = this.comments.filter(c => c.id !== commentUpdates.commentId);
    this.createCommentThreads();
  }

  private removeAllAutoGeneratedComments() {
    this.comments = this.comments.filter(c => c.createdBy !== 'azure-sdk');
    this.createCommentThreads();
  }

  private toggleCommentUpVote(commentUpdates: CommentUpdatesDto) {
    const comment = this.comments.find(c => c.id === commentUpdates.commentId)
    if (comment) {
      if (comment.upvotes.includes(this.userProfile?.userName!)) {
        comment.upvotes.splice(comment.upvotes.indexOf(this.userProfile?.userName!), 1);
      } else {
        comment.upvotes.push(this.userProfile?.userName!);
        if (comment.downvotes.includes(this.userProfile?.userName!)) {
          comment.downvotes.splice(comment.downvotes.indexOf(this.userProfile?.userName!), 1);
        }
      }
    }
  }

  private toggleCommentDownVote(commentUpdates: CommentUpdatesDto) {
    const comment = this.comments.find(c => c.id === commentUpdates.commentId)
    if (comment) {
      if (comment.downvotes.includes(this.userProfile?.userName!)) {
        comment.downvotes.splice(comment.downvotes.indexOf(this.userProfile?.userName!), 1);
      } else {
        comment.downvotes.push(this.userProfile?.userName!);
        if (comment.upvotes.includes(this.userProfile?.userName!)) {
          comment.upvotes.splice(comment.upvotes.indexOf(this.userProfile?.userName!), 1);
        }
      }
    }
  }

  // --- Filter methods ---

  private updateUnknownSeverityFilterVisibility(): void {
    if (!this.hasAnyUnknownThreads) {
      this.showUnknownSeverityFilter = false;
      this.filterSeverities.delete(UNKNOWN_SEVERITY_KEY);
      return;
    }

    // Compute Unknown chip visibility from the current status/kind context.
    // This does not depend on currently selected severity chips.
    this.showUnknownSeverityFilter = false;
    for (const threads of this.commentThreads.values()) {
      for (const thread of threads) {
        if (this.threadMatchesStatusAndKindFilters(thread) && this.getDisplayedThreadSeverityKey(thread) === UNKNOWN_SEVERITY_KEY) {
          this.showUnknownSeverityFilter = true;
          break;
        }
      }
      if (this.showUnknownSeverityFilter) {
        break;
      }
    }

    if (!this.showUnknownSeverityFilter) {
      this.filterSeverities.delete(UNKNOWN_SEVERITY_KEY);
    }
  }

  applyFilters() {
    this.filteredCommentThreads = new Map();
    this.totalThreadCount = 0;
    this.filteredThreadCount = 0;

    this.commentThreads.forEach((threads, revisionId) => {
      this.totalThreadCount += threads.length;
      const filtered = threads.filter(thread => this.threadMatchesFilters(thread));
      if (filtered.length > 0) {
        this.filteredCommentThreads.set(revisionId, filtered);
      }
      this.filteredThreadCount += filtered.length;
    });

    this.filteredApiRevisionsWithComments = this.apiRevisionsWithComments.filter(
      rev => this.filteredCommentThreads.has(rev.id)
    );
  }

  private threadMatchesFilters(thread: CodePanelRowData): boolean {
    // First check status and kind
    if (!this.threadMatchesStatusAndKindFilters(thread)) return false;

    // Then check severity (empty set = show all)
    if (this.filterSeverities.size > 0) {
      const normalizedSev = this.getDisplayedThreadSeverityKey(thread);
      if (!this.filterSeverities.has(normalizedSev)) return false;
    }

    return true;
  }

  getThreadKind(comment: CommentItemModel): 'human' | 'ai' | 'diagnostic' {
    if (comment.commentSource === CommentSource.Diagnostic) return 'diagnostic';
    if (comment.commentSource === CommentSource.AIGenerated || comment.createdBy === 'azure-sdk') return 'ai';
    return 'human';
  }

  setStatusFilter(status: 'all' | 'active' | 'resolved') {
    this.filterStatus = status;
    this.updateUnknownSeverityFilterVisibility();
    this.applyFilters();
    this.changeDetectorRef.markForCheck();
  }

  toggleSeverityFilter(severityKey: string) {
    if (this.filterSeverities.has(severityKey)) {
      this.filterSeverities.delete(severityKey);
    } else {
      this.filterSeverities.add(severityKey);
    }
    this.applyFilters();
    this.changeDetectorRef.markForCheck();
  }

  toggleKindFilter(kind: 'human' | 'ai' | 'diagnostic') {
    if (this.filterKinds.has(kind)) {
      this.filterKinds.delete(kind);
    } else {
      this.filterKinds.add(kind);
    }
    this.updateUnknownSeverityFilterVisibility();
    this.applyFilters();
    this.changeDetectorRef.markForCheck();
  }

  clearAllFilters() {
    this.filterStatus = 'active';
    this.filterSeverities.clear();
    this.filterKinds.clear();
    this.updateUnknownSeverityFilterVisibility();
    this.applyFilters();
    this.changeDetectorRef.markForCheck();
  }

  get hasActiveFilters(): boolean {
    return this.filterStatus !== 'active' || this.filterSeverities.size > 0 || this.filterKinds.size > 0;
  }

  getSeverityLabel(severity: CommentSeverity | string | null | undefined): string {
    return CommentSeverityHelper.getSeverityLabel(severity);
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
