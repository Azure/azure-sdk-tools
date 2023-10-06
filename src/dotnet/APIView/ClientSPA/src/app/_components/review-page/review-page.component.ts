import { Component, Input, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { NavigationItem, Review, ReviewContent, ReviewLine } from 'src/app/_models/review';
import { Revision } from 'src/app/_models/revision';
import { ReviewsService } from 'src/app/_services/reviews/reviews.service';

@Component({
  selector: 'app-review-page',
  templateUrl: './review-page.component.html',
  styleUrls: ['./review-page.component.scss']
})
export class ReviewPageComponent implements OnInit {
  review: Review | undefined = undefined;
  navigation: NavigationItem[] = []
  reviewLines: ReviewLine [] = [];
  reviewRevisions : Map<string, Revision[]> = new Map<string, Revision[]>();
  activeRevision : Revision | undefined = undefined;

  revisionSidePanel : boolean | undefined = undefined;

  constructor(private route: ActivatedRoute, private reviewsService: ReviewsService) {}

  ngOnInit() {
    const reviewId = this.route.snapshot.paramMap.get('reviewId');
    const revisionId = this.route.snapshot.queryParamMap.get('revisionId');
    if (reviewId && revisionId) {
      this.loadReviewContent(reviewId, revisionId);
    }
    else if (reviewId) {
      this.loadReviewContent(reviewId);
    }
  }

  loadReviewContent(reviewId: string, revisionId: string | undefined = undefined) {
    this.reviewsService.getReviewContent(reviewId, revisionId).subscribe({
      next: (response: ReviewContent) => {
          this.review = response.review;
          this.navigation = response.navigation;
          this.reviewLines = response.codeLines;
          this.reviewRevisions = new Map(Object.entries(response.reviewRevisions));
          this.activeRevision = response.activeRevision;
        }
    });
  }

  showRevisionsPanel(showRevisionsPanel : any){
    this.revisionSidePanel = showRevisionsPanel as boolean;
  }

  onRevisionSelect(revision: Revision) {
    this.activeRevision = revision;
  }
}
