import { Component } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { MenuItem } from 'primeng/api';
import { Subject, takeUntil } from 'rxjs';
import { REVIEW_ID_ROUTE_PARAM } from 'src/app/_helpers/common-helpers';
import { Review } from 'src/app/_models/review';
import { UserProfile } from 'src/app/_models/userProfile';
import { ReviewsService } from 'src/app/_services/reviews/reviews.service';
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

  private destroy$ = new Subject<void>();

  constructor(private route: ActivatedRoute, private reviewsService: ReviewsService, private userProfileService: UserProfileService) {}

  ngOnInit() {
    this.userProfileService.getUserProfile().subscribe();
    this.reviewId = this.route.snapshot.paramMap.get(REVIEW_ID_ROUTE_PARAM);
    this.createSideMenu();
    this.loadReview(this.reviewId!);
  }

  createSideMenu() {
    this.sideMenu = [
      {
        icon: 'bi bi-clock-history',
        //command: () => { this.revisionSidePanel = !this.revisionSidePanel; }
      },
      {
        icon: 'bi bi-chat-left-dots',
        //command: () => { this.conversationSidePanel = !this.conversationSidePanel; }
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
}
