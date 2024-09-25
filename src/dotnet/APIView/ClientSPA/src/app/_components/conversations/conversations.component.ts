import { Component, EventEmitter, Input, OnChanges, OnInit, Output, SimpleChanges } from '@angular/core';
import { CodePanelRowData, CodePanelRowDatatype } from 'src/app/_models/codePanelModels';
import { CommentItemModel, CommentType } from 'src/app/_models/commentItemModel';
import { APIRevision } from 'src/app/_models/revision';
import { getTypeClass } from 'src/app/_helpers/common-helpers';
import { CommentsService } from 'src/app/_services/comments/comments.service';
import { Subject, take, takeUntil } from 'rxjs';
import { Review } from 'src/app/_models/review';
import { UserProfile } from 'src/app/_models/userProfile';
import { CommentThreadUpdateAction, CommentUpdatesDto } from 'src/app/_dtos/commentThreadUpdateDto';
import { SignalRService } from 'src/app/_services/signal-r/signal-r.service';

@Component({
  selector: 'app-conversations',
  templateUrl: './conversations.component.html',
  styleUrls: ['./conversations.component.scss']
})
export class ConversationsComponent implements OnChanges {
  @Input() apiRevisions: APIRevision[] = [];
  @Input() activeApiRevisionId: string | null = null;
  @Input() comments: CommentItemModel[] = [];
  @Input() review : Review | undefined = undefined;
  @Input() userProfile : UserProfile | undefined;

  @Output() scrollToNodeEmitter : EventEmitter<string> = new EventEmitter<string>();
  @Output() numberOfActiveThreadsEmitter : EventEmitter<number> = new EventEmitter<number>();

  commentThreads: Map<string, CodePanelRowData[]> = new Map<string, CodePanelRowData[]>();
  numberOfActiveThreads: number = 0;

  apiRevisionsLoaded = false;
  commentsLoaded = false;
  isLoading: boolean = true;

  destroy$ = new Subject<void>();

  constructor(private commentsService: CommentsService, private signalRService: SignalRService) { }

  ngOnInit() {
    this.handleRealTimeCommentUpdates();
  }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['apiRevisions']) {
      this.apiRevisionsLoaded = true;
    }

    if (changes['comments']) {
      this.commentsLoaded = true;
    }

    if (this.apiRevisionsLoaded && this.commentsLoaded) {
      this.createCommentThreads();
    }
  }

  createCommentThreads() {
    if (this.apiRevisions.length > 0 && this.comments.length > 0) {
      this.commentThreads = new Map<string, CodePanelRowData[]>();
      this.numberOfActiveThreads = 0;
      const apiRevisionInOrder = this.apiRevisions.sort((a, b) => (new Date(b.createdOn) as any) - (new Date(a.createdOn) as any));
      const groupedComments = this.comments
        .reduce((acc: { [key: string]: CommentItemModel[] }, comment) => {
          const key = comment.elementId;
          if (!acc[key]) {
            acc[key] = [];
          }
          acc[key].push(comment);
          return acc;
        }, {});

      for (const elementId in groupedComments) {
        if (groupedComments.hasOwnProperty(elementId)) {
          const comments = groupedComments[elementId];
          const apiRevisionIds = comments.map(c => c.apiRevisionId);

          let apiRevisionPostion = Number.MAX_SAFE_INTEGER;

          for (const apiRevisionId of apiRevisionIds) {
            const apiRevisionIdPosition = apiRevisionInOrder.findIndex(apiRevision => apiRevision.id === apiRevisionId);
            if (apiRevisionIdPosition >= 0 && apiRevisionIdPosition < apiRevisionPostion) {
              apiRevisionPostion = apiRevisionIdPosition;
            }
          }

          if (apiRevisionPostion >= 0 && apiRevisionPostion < apiRevisionInOrder.length) {
            const apiRevisionIdForThread = apiRevisionInOrder[apiRevisionPostion].id;
            const codePanelRowData = new CodePanelRowData();
            codePanelRowData.type = CodePanelRowDatatype.CommentThread;
            codePanelRowData.comments = comments;
            codePanelRowData.isResolvedCommentThread = comments.some(c => c.isResolved);

            if (!codePanelRowData.isResolvedCommentThread) {
              this.numberOfActiveThreads++;
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
      this.isLoading = false;
    }
    else if (this.apiRevisions.length > 0 && this.comments.length === 0) {
      setTimeout(() => {
        this.isLoading = false;
      }, 1000);
    }
  }

  getAPIRevisionWithComments() {
    return this.apiRevisions.filter(apiRevision => this.commentThreads.has(apiRevision.id));
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
      window.open(`review/${this.review?.id}?activeApiRevisionId=${revisionIdForConversationGroup}&nId=${elementIdForConversationGroup}`, '_blank');
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
            case CommentThreadUpdateAction.CommentDeleted:
              this.deleteCommentFromCommentThread(commentUpdates);
              break;
          }
        }
      }
    });
  }
  
  handleSaveCommentActionEmitter(commentUpdates: CommentUpdatesDto) {
    commentUpdates.reviewId = this.review?.id!;
    if (commentUpdates.commentId) {
      this.commentsService.updateComment(this.review?.id!, commentUpdates.commentId, commentUpdates.commentText!).pipe(take(1)).subscribe({
        next: () => {
          this.updateCommentTextInCommentThread(commentUpdates);
          this.signalRService.pushCommentUpdates(commentUpdates);
        }
      });
    }
    else {
      this.commentsService.createComment(this.review?.id!, commentUpdates.revisionId!, commentUpdates.elementId!, commentUpdates.commentText!, CommentType.APIRevision, commentUpdates.allowAnyOneToResolve)
        .pipe(take(1)).subscribe({
            next: (response: CommentItemModel) => {
              commentUpdates.comment = response;
              this.addCommentToCommentThread(commentUpdates);
              this.signalRService.pushCommentUpdates(commentUpdates);
            }
          }
        );
    }
  }

  handleCommentUpvoteActionEmitter(commentUpdates: CommentUpdatesDto){
    commentUpdates.reviewId = this.review?.id!;
    this.commentsService.toggleCommentUpVote(this.review?.id!, commentUpdates.commentId!).pipe(take(1)).subscribe({
      next: () => {
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
      }
    });
  }

  handleCommentResolutionActionEmitter(commentUpdates: CommentUpdatesDto) {
    commentUpdates.reviewId = this.review?.id!;
    if (commentUpdates.commentThreadUpdateAction === CommentThreadUpdateAction.CommentResolved) {
      this.commentsService.resolveComments(this.review?.id!, commentUpdates.elementId!).pipe(take(1)).subscribe({
        next: () => {
          this.applyCommentResolutionUpdate(commentUpdates);
          this.signalRService.pushCommentUpdates(commentUpdates);
        }
      });
    }
    if (commentUpdates.commentThreadUpdateAction === CommentThreadUpdateAction.CommentUnResolved) {
      this.commentsService.unresolveComments(this.review?.id!, commentUpdates.elementId!).pipe(take(1)).subscribe({
        next: () => {
          this.applyCommentResolutionUpdate(commentUpdates);
          this.signalRService.pushCommentUpdates(commentUpdates);
        }
      });
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
    this.comments.filter(c => c.elementId === commentUpdates.elementId).forEach(c => {
      c.isResolved = (commentUpdates.commentThreadUpdateAction === CommentThreadUpdateAction.CommentResolved)? true : false;
    });
    this.createCommentThreads();
  }

  private deleteCommentFromCommentThread(commentUpdates: CommentUpdatesDto) {
    this.comments = this.comments.filter(c => c.id !== commentUpdates.commentId);
    this.createCommentThreads();
  }

  private toggleCommentUpVote(commentUpdates: CommentUpdatesDto) {
    const comment = this.comments.find(c => c.id === commentUpdates.commentId)
    if (comment) {
      if (comment.upvotes.includes(this.userProfile?.userName!)) {
        comment.upvotes.splice(comment.upvotes.indexOf(this.userProfile?.userName!), 1);
      } else {
        comment.upvotes.push(this.userProfile?.userName!);
      }
    }
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
