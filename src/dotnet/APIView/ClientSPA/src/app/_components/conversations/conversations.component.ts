import { Component, Input, OnChanges, OnInit, SimpleChanges } from '@angular/core';
import { CodePanelRowData, CodePanelRowDatatype } from 'src/app/_models/codePanelModels';
import { CommentItemModel } from 'src/app/_models/commentItemModel';
import { APIRevision } from 'src/app/_models/revision';

@Component({
  selector: 'app-conversations',
  templateUrl: './conversations.component.html',
  styleUrls: ['./conversations.component.scss']
})
export class ConversationsComponent implements OnChanges {
  @Input() apiRevisions: APIRevision[] = [];
  @Input() comments: CommentItemModel[] = [];

  commentThreads: Map<string, CodePanelRowData[]> = new Map<string, CodePanelRowData[]>();

  ngOnChanges(changes: SimpleChanges) {
    if (changes['apiRevisions'] || changes['comments']) {
      if (this.apiRevisions.length > 0 && this.comments.length > 0) {
        this.createCommentThreads();
      }
    }
  }

  createCommentThreads() {
    for (const apiRevision of this.apiRevisions) {
      const groupedCommentsForAPIRevision = this.comments
        .filter(c => c.apiRevisionId === apiRevision.id)
        .reduce((acc: { [key: string]: CommentItemModel[] }, comment) => {
        const key = comment.elementId;
        if (!acc[key]) {
          acc[key] = [];
        }
        acc[key].push(comment);
        return acc;
      }, {});

      if (Object.keys(groupedCommentsForAPIRevision).length > 0) {
        this.commentThreads.set(apiRevision.id, []);
      }

      for (const elementId in groupedCommentsForAPIRevision) {
        if (groupedCommentsForAPIRevision.hasOwnProperty(elementId)) {
          const comments = groupedCommentsForAPIRevision[elementId];
          const codePanelRowData = new CodePanelRowData();
          codePanelRowData.type = CodePanelRowDatatype.CommentThread;
          codePanelRowData.comments = comments;
          codePanelRowData.isResolvedCommentThread = comments.some(c => c.isResolved);
          this.commentThreads.get(apiRevision.id)?.push(codePanelRowData);
        }
      }
    }
  }

  getAPIRevisionWithComments() {
    return this.apiRevisions.filter(apiRevision => this.commentThreads.has(apiRevision.id));
  }
}
