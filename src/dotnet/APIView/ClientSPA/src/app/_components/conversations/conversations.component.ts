import { Component, Input, OnChanges, OnInit, SimpleChanges } from '@angular/core';
import { CodePanelRowData, CodePanelRowDatatype } from 'src/app/_models/codePanelModels';
import { CommentItemModel, CommentType } from 'src/app/_models/commentItemModel';
import { APIRevision } from 'src/app/_models/revision';
import { getTypeClass } from 'src/app/_helpers/common-helpers';
import { CommentsService } from 'src/app/_services/comments/comments.service';
import { take } from 'rxjs';
import { Review } from 'src/app/_models/review';

@Component({
  selector: 'app-conversations',
  templateUrl: './conversations.component.html',
  styleUrls: ['./conversations.component.scss']
})
export class ConversationsComponent implements OnChanges {
  @Input() apiRevisions: APIRevision[] = [];
  @Input() comments: CommentItemModel[] = [];
  @Input() review : Review | undefined = undefined;

  commentThreads: Map<string, CodePanelRowData[]> = new Map<string, CodePanelRowData[]>();

  constructor(private commentsService: CommentsService) { }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['apiRevisions'] || changes['comments']) {
      if (this.apiRevisions.length > 0 && this.comments.length > 0) {
        this.createCommentThreads();
      }
    }
  }

  createCommentThreads() {
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

          if (this.commentThreads.has(apiRevisionIdForThread)) {
            this.commentThreads.get(apiRevisionIdForThread)?.push(codePanelRowData);
          }
          else {
          this.commentThreads.set(apiRevisionIdForThread, [codePanelRowData]);
          }
        }
      }
    }
  }

  getAPIRevisionWithComments() {
    return this.apiRevisions.filter(apiRevision => this.commentThreads.has(apiRevision.id));
  }

  getAPIRevisionTypeClass(apiRevision: APIRevision) {
    return getTypeClass(apiRevision.apiRevisionType);
  }

  
  handleSaveCommentActionEmitter(data: any) {
    console.log(data);
    //if (data.commentId) {
    //  this.commentsService.updateComment(this.review?.id!, data.commentId, data.commentText).pipe(take(1)).subscribe({
    //    next: () => {
    //      //this.updateHasActiveConversations();
    //    }
    //  });
    //}
    //else {
    //  this.commentsService.createComment(this.review?.id!, data.conversationGroupId!, data.nodeId, data.commentText, CommentType.APIRevision, data.allowAnyOneToResolve)
    //    .pipe(take(1)).subscribe({
    //        next: (response: CommentItemModel) => {
    //          //this.updateHasActiveConversations();
    //        }
    //      }
    //    );
    //}
  }
}
