import { Component, OnInit } from '@angular/core';
import { TablePageEvent } from 'primeng/table';
import { Review } from 'src/app/_models/review';
import { ReviewsService } from 'src/app/_services/reviews/reviews.service';

@Component({
  selector: 'app-reviews-list',
  templateUrl: './reviews-list.component.html',
  styleUrls: ['./reviews-list.component.scss']
})
export class ReviewsListComponent implements OnInit {
  totalNumberOfReviews: number = 0;
  fetchedReviews: Review [] = [];
  rows : number = 25;

  constructor(private reviewsService: ReviewsService) { }

  ngOnInit(): void {
    this.loadReviews(0, this.rows * 2);
  }

  loadReviews(offset: number, limit: number) {
    this.reviewsService.getReviews(offset, limit).subscribe({
      next: reviewList => {
        this.fetchedReviews = reviewList.reviews;
        this.totalNumberOfReviews = reviewList.totalNumberOfReviews;
      },
    });
  }

  onPage(event: TablePageEvent) {

  }
}
