import { Component } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { Title } from '@angular/platform-browser';
import { Subject, takeUntil } from 'rxjs';
import { REVIEW_ID_ROUTE_PARAM, ACTIVE_API_REVISION_ID_QUERY_PARAM } from 'src/app/_helpers/router-helpers';
import { Review } from 'src/app/_models/review';
import { APIRevision } from 'src/app/_models/revision';
import { ReviewsService } from 'src/app/_services/reviews/reviews.service';
import { ReviewPageLayoutModule } from 'src/app/_modules/shared/review-page-layout.module';
import { SharedAppModule } from 'src/app/_modules/shared/shared-app.module';
import { CommonModule } from '@angular/common';

@Component({
    selector: 'app-revision-page',
    templateUrl: './revision-page.component.html',
    styleUrls: ['./revision-page.component.scss'],
    standalone: true,
    imports: [CommonModule, ReviewPageLayoutModule, SharedAppModule]
})
export class RevisionPageComponent {
  reviewId : string | null = null;
  review : Review | undefined = undefined;
  apiRevisions: APIRevision[] = [];
  activeApiRevisionId: string | null = null;

  private destroy$ = new Subject<void>();

  constructor(private route: ActivatedRoute, private reviewsService: ReviewsService, private router: Router, private titleService: Title) {}

  ngOnInit() {
    this.reviewId = this.route.snapshot.paramMap.get(REVIEW_ID_ROUTE_PARAM);
    this.activeApiRevisionId = this.route.snapshot.queryParamMap.get(ACTIVE_API_REVISION_ID_QUERY_PARAM);
    this.loadReview(this.reviewId!);
  }

  loadReview(reviewId: string) {
    this.reviewsService.getReview(reviewId)
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: (review: Review) => {
          this.review = review;
          this.updatePageTitle();
        }
    });
  }

  navigateToReview() {
    const queryParams: any = {};
    if (this.activeApiRevisionId) {
      queryParams['activeApiRevisionId'] = this.activeApiRevisionId;
    }
    this.router.navigate(['/review', this.reviewId], { queryParams: queryParams });
  }

  navigateToSamples() {
    const queryParams: any = {};
    if (this.activeApiRevisionId) {
      queryParams['activeApiRevisionId'] = this.activeApiRevisionId;
    }
    this.router.navigate(['/samples', this.reviewId], { queryParams: queryParams });
  }

  navigateToConversations() {
    const queryParams: any = {};
    if (this.activeApiRevisionId) {
      queryParams['activeApiRevisionId'] = this.activeApiRevisionId;
    }
    queryParams['view'] = 'conversations';
    this.router.navigate(['/review', this.reviewId], { queryParams: queryParams });
  }

  noop() { }

  handleApiRevisionsEmitter(apiRevisions: APIRevision[]) {
    this.apiRevisions = apiRevisions as APIRevision[];
  }

  updatePageTitle() {
    if (this.review?.packageName) {
      this.titleService.setTitle(this.review.packageName);
    } else {
      this.titleService.setTitle('APIView');
    }
  }
}
