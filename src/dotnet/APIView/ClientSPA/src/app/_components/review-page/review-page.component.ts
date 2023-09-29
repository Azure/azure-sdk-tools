import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { CodeLine, NavigationItem, Review, ReviewContent } from 'src/app/_models/review';
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
  codeLines: CodeLine [] = [];
  reviewRevisions : Map<string, Revision[]> = new Map<string, Revision[]>();
  activeRevision : Revision | undefined = undefined;
  

  constructor(private route: ActivatedRoute, private reviewsService: ReviewsService) {}

  ngOnInit() {
    const reviewId = this.route.snapshot.paramMap.get('reviewId');
    this.loadReviewContent(reviewId!) 
  }

  loadReviewContent(reviewId: string) {
    this.reviewsService.getReviewContent(reviewId).subscribe({
      next: (response: ReviewContent) => {
          this.review = response.review;
          this.navigation = response.navigation;
          this.codeLines = response.codeLines;
          this.reviewRevisions = new Map(Object.entries(response.reviewRevisions));
          this.activeRevision = response.activeRevision;
        }
    });
  }
}
