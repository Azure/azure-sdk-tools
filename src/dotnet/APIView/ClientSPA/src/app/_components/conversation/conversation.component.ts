import { Component, Input } from '@angular/core';
import { CommentItemModel } from 'src/app/_models/commentItemModel';
import { APIRevision } from 'src/app/_models/revision';

@Component({
  selector: 'app-conversation',
  templateUrl: './conversation.component.html',
  styleUrls: ['./conversation.component.scss']
})
export class ConversationComponent {
  @Input() apiRevisions: APIRevision[] = [];
  @Input() comments: CommentItemModel[] = [];

  getFilteredComments(apiRevision: APIRevision): CommentItemModel[] {
    return this.comments.filter(c => c.aPIRevisionId === apiRevision.id);
  }
}
