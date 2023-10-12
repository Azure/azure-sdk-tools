import { Component } from '@angular/core';
import { FirstReleaseApproval, Review } from 'src/app/_models/review';

@Component({
  selector: 'app-index-page',
  templateUrl: './index-page.component.html',
  styleUrls: ['./index-page.component.scss']
})
export class IndexPageComponent {
  review : Review | null = null;

  /**
   * Pass ReviewId to revision component to load revisions
   *  * @param reviewId
   */
  getRevisions(review: Review) {
    this.review = review;
  }
}
