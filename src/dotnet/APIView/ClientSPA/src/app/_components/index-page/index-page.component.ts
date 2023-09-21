import { Component } from '@angular/core';

@Component({
  selector: 'app-index-page',
  templateUrl: './index-page.component.html',
  styleUrls: ['./index-page.component.scss']
})
export class IndexPageComponent {
  reviewId : string = "";

  /**
   * Pass ReviewId to revision component to load revisions
   *  * @param reviewId
   */
  getRevisions(reviewId: string) {
    this.reviewId = reviewId;
  }

}
