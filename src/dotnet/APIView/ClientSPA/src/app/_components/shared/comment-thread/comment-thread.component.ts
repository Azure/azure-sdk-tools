import { Component, Injector, Input } from '@angular/core';
import { CommentItemModel } from 'src/app/_models/review';

@Component({
  selector: 'app-comment-thread',
  templateUrl: './comment-thread.component.html',
  styleUrls: ['./comment-thread.component.scss'],
  host: {
    'class': 'comment-thread'
  }
})
export class CommentThreadComponent {
  @Input() comments: CommentItemModel[] | undefined = []
  commentEditText: string | undefined;
}
