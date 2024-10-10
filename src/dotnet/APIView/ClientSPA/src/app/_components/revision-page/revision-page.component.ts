import { Component } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { MenuItem } from 'primeng/api';
import { Subject, takeUntil } from 'rxjs';
import { REVIEW_ID_ROUTE_PARAM } from 'src/app/_helpers/router-helpers';
import { Review } from 'src/app/_models/review';
import { APIRevision } from 'src/app/_models/revision';
import { ReviewsService } from 'src/app/_services/reviews/reviews.service';
import { APIRevisionsService } from 'src/app/_services/revisions/revisions.service';

@Component({
  selector: 'app-revision-page',
  templateUrl: './revision-page.component.html',
  styleUrls: ['./revision-page.component.scss']
})
export class RevisionPageComponent {
  reviewId : string | null = null;
  review : Review | undefined = undefined;
  sideMenu: MenuItem[] | undefined;
  apiRevisions: APIRevision[] = [];

  private destroy$ = new Subject<void>();

  constructor(private route: ActivatedRoute, private reviewsService: ReviewsService, private apiRevisionsService: APIRevisionsService, private router: Router) {}

  ngOnInit() {
    this.reviewId = this.route.snapshot.paramMap.get(REVIEW_ID_ROUTE_PARAM);
    this.createSideMenu();
    this.loadReview(this.reviewId!);
  }

  createSideMenu() {
    this.sideMenu = [
      {
        icon: 'bi bi-braces',
        tooltip: 'API',
        command: () => this.openLatestAPIReivisonForReview()
      },
      {
        icon: 'bi bi-chat-left-dots',
        tooltip: 'Conversations',
        command: () => this.router.navigate([`/conversation/${this.reviewId}`])
      }
    ];
  }

  loadReview(reviewId: string) {
    this.reviewsService.getReview(reviewId)
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: (review: Review) => {
          this.review = review;
        }
    });
  }

  openLatestAPIReivisonForReview() {
    const apiRevision = this.apiRevisions.find(x => x.apiRevisionType === "Automatic") ?? this.apiRevisions[0];
    this.apiRevisionsService.openAPIRevisionPage(apiRevision, this.route);
  }

  handleApiRevisionsEmitter(apiRevisions: APIRevision[]) {
    this.apiRevisions = apiRevisions as APIRevision[];
  }
}
