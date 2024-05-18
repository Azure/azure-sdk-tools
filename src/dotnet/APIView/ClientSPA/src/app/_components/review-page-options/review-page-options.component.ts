import { Component } from '@angular/core';

@Component({
  selector: 'app-review-page-options',
  templateUrl: './review-page-options.component.html',
  styleUrls: ['./review-page-options.component.scss']
})
export class ReviewPageOptionsComponent {
  showComments : boolean = true;
  showSystemComments: boolean = true;
  showDocumentation: boolean = true;
  showLineNumber: boolean = true;
  showLeftNavigation: boolean = true;
  showOnlyDiff : boolean = false;

}
