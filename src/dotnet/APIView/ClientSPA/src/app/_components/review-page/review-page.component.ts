import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { CodeLine, NavigationItem, ReviewContent } from 'src/app/_models/review';
import { ReviewsService } from 'src/app/_services/reviews/reviews.service';

@Component({
  selector: 'app-review-page',
  templateUrl: './review-page.component.html',
  styleUrls: ['./review-page.component.scss']
})
export class ReviewPageComponent implements OnInit {
  navigation: NavigationItem[] = []
  codeLines: CodeLine [] = [];

  constructor(private route: ActivatedRoute, private reviewsService: ReviewsService) {}

  ngOnInit() {
    const reviewId = this.route.snapshot.paramMap.get('reviewId');
    this.loadReviewContent(reviewId!) 
  }

  loadReviewContent(reviewId: string) {
    this.reviewsService.getReviewContent(reviewId).subscribe({
      next: (response: ReviewContent) => {
          this.navigation = response.navigation;
          this.codeLines = response.codeLines;
        }
    });
  }
}
