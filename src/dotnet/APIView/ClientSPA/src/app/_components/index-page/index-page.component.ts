import { Component } from '@angular/core';
import { Review } from 'src/app/_models/review';

@Component({
  selector: 'app-index-page',
  templateUrl: './index-page.component.html',
  styleUrls: ['./index-page.component.scss']
})
export class IndexPageComponent {
  review : Review | undefined = undefined;

  /**
   * Pass ReviewId to revision component to load revisions
   *  * @param reviewId
   */
  getRevisions(review: Review) {
    this.review = review;
  }
}
