import { Component } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { MenuItem } from 'primeng/api';
import { Subject, takeUntil } from 'rxjs';
import { REVIEW_ID_ROUTE_PARAM } from 'src/app/_helpers/router-helpers';
import { CommentItemModel, CommentType } from 'src/app/_models/commentItemModel';
import { Review } from 'src/app/_models/review';
import { APIRevision } from 'src/app/_models/revision';
import { UserProfile } from 'src/app/_models/userProfile';
import { CommentsService } from 'src/app/_services/comments/comments.service';
import { ReviewsService } from 'src/app/_services/reviews/reviews.service';
import { APIRevisionsService } from 'src/app/_services/revisions/revisions.service';
import { UserProfileService } from 'src/app/_services/user-profile/user-profile.service';

@Component({
  selector: 'app-conversation-page',
  templateUrl: './conversation-page.component.html',
  styleUrls: ['./conversation-page.component.scss']
})
export class ConversationPageComponent {
  reviewId : string | null = null;
  review : Review | undefined = undefined;
  userProfile : UserProfile | undefined;
  sideMenu: MenuItem[] | undefined;
  comments: CommentItemModel[] = [];
  apiRevisions: APIRevision[] = [];

  apiRevisionPageSize = 50;

  private destroy$ = new Subject<void>();

  constructor(private route: ActivatedRoute, private reviewsService: ReviewsService, private userProfileService: UserProfileService,
    private apiRevisionsService: APIRevisionsService, private commentsService: CommentsService
  ) {}

  ngOnInit() {
    this.userProfileService.getUserProfile().subscribe(
      (userProfile : any) => {
        this.userProfile = userProfile;
      }
    );
    this.reviewId = this.route.snapshot.paramMap.get(REVIEW_ID_ROUTE_PARAM);
    this.createSideMenu();
    this.loadReview(this.reviewId!);
    this.loadAPIRevisions(0, this.apiRevisionPageSize);
    this.loadComments();
  }

  createSideMenu() {
    this.sideMenu = [
      {
        icon: 'bi bi-braces',
        tooltip: 'API',
        command: () => this.openLatestAPIReivisonForReview()
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

  loadAPIRevisions(noOfItemsRead : number, pageSize: number) {
    this.apiRevisionsService.getAPIRevisions(noOfItemsRead, pageSize, this.reviewId!, undefined, undefined, 
      undefined, "createdOn", undefined, undefined, undefined, true)
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: (response: any) => {
          this.apiRevisions = response.result;
        }
    });
  }

  loadComments() {
    this.commentsService.getComments(this.reviewId!, CommentType.APIRevision)
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: (comments: CommentItemModel[]) => {
          this.comments = comments;
        }
    });
  }

  openLatestAPIReivisonForReview() {
    const apiRevision = this.apiRevisions.find(x => x.apiRevisionType === "Automatic") ?? this.apiRevisions[0];
    this.apiRevisionsService.openAPIRevisionPage(apiRevision, this.route);
  }
}
