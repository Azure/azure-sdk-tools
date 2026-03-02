import { ChangeDetectionStrategy, ChangeDetectorRef, Component, EventEmitter, Input, OnChanges, OnDestroy, Output, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TimeagoModule } from 'ngx-timeago';
import { TimelineModule } from 'primeng/timeline';
import { CommentThreadComponent } from '../shared/comment-thread/comment-thread.component';
import { LastUpdatedOnPipe } from 'src/app/_pipes/last-updated-on.pipe';
import { CodePanelRowData, CodePanelRowDatatype } from 'src/app/_models/codePanelModels';
import { CommentItemModel, CommentType, CommentSource } from 'src/app/_models/commentItemModel';
import { APIRevision } from 'src/app/_models/revision';
import { getTypeClass } from 'src/app/_helpers/common-helpers';
import { getVisibleComments } from 'src/app/_helpers/comment-visibility.helper';
import { CommentsService } from 'src/app/_services/comments/comments.service';
import { Subject, take, takeUntil } from 'rxjs';
import { Review } from 'src/app/_models/review';
import { UserProfile } from 'src/app/_models/userProfile';
import { CommentThreadUpdateAction, CommentUpdatesDto } from 'src/app/_dtos/commentThreadUpdateDto';
import { SignalRService } from 'src/app/_services/signal-r/signal-r.service';

@Component({
    selector: 'app-conversations',
    templateUrl: './conversations.component.html',
    styleUrls: ['./conversations.component.scss'],
    standalone: true,
    imports: [
        CommonModule,
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

  @Output() scrollToNodeEmitter : EventEmitter<string> = new EventEmitter<string>();
  @Output() numberOfActiveThreadsEmitter : EventEmitter<number> = new EventEmitter<number>();
  @Output() dismissSidebarAndNavigateEmitter : EventEmitter<{revisionId: string, elementId: string}> = new EventEmitter<{revisionId: string, elementId: string}>();

  private readonly MAX_DIAGNOSTICS_DISPLAY = 250;

  commentThreads: Map<string, CodePanelRowData[]> = new Map<string, CodePanelRowData[]>();
  numberOfActiveThreads: number = 0;
  // Flag to indicate if diagnostics were truncated due to limit
  diagnosticsTruncated: boolean = false;
  totalDiagnosticsInRevision: number = 0;

  apiRevisionsWithComments: APIRevision[] = [];

  apiRevisionsLoaded = false;
  commentsLoaded = false;
  isLoading: boolean = true;

  destroy$ = new Subject<void>();

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

      // Use shared visibility logic — single source of truth for which comments are relevant
      const { allVisibleComments, diagnosticCommentsForRevision } = getVisibleComments(this.comments, this.activeApiRevisionId);

      this.totalDiagnosticsInRevision = diagnosticCommentsForRevision.length;
      this.diagnosticsTruncated = diagnosticCommentsForRevision.length > this.MAX_DIAGNOSTICS_DISPLAY;

      // For display, cap diagnostics at MAX_DIAGNOSTICS_DISPLAY; for badge counts, use the full set
      const limitedDiagnostics = diagnosticCommentsForRevision.slice(0, this.MAX_DIAGNOSTICS_DISPLAY);
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

      this.isLoading = false;
      this.changeDetectorRef.markForCheck();
    }
    else if (this.apiRevisions.length > 0 && this.comments.length === 0) {
      this.apiRevisionsWithComments = [];
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

  navigateToCommentThreadOnRevisionPage(event: Event) {
    const target = event.target as Element;
    const revisionIdForConversationGroup = target.closest(".conversation-group-revision-id")?.getAttribute("data-conversation-group-revision-id");
    const elementIdForConversationGroup = (target.closest(".conversation-group-threads")?.getElementsByClassName("conversation-group-element-id")[0] as HTMLElement).innerText;

    if (this.activeApiRevisionId && this.activeApiRevisionId === revisionIdForConversationGroup) {
      this.scrollToNodeEmitter.emit(elementIdForConversationGroup);
    } else {
      this.dismissSidebarAndNavigateEmitter.emit({
        revisionId: revisionIdForConversationGroup!,
        elementId: elementIdForConversationGroup
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
    if (commentUpdates.commentThreadUpdateAction === CommentThreadUpdateAction.CommentResolved) {
      this.commentsService.resolveComments(this.review?.id!, commentUpdates.elementId!, commentUpdates.threadId).pipe(take(1)).subscribe({
        next: () => {
          this.applyCommentResolutionUpdate(commentUpdates);
          this.commentsService.notifyQualityScoreRefresh();
        }
      });
    }
    if (commentUpdates.commentThreadUpdateAction === CommentThreadUpdateAction.CommentUnResolved) {
      this.commentsService.unresolveComments(this.review?.id!, commentUpdates.elementId!, commentUpdates.threadId).pipe(take(1)).subscribe({
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

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
