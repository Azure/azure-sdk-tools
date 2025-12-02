import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { take } from 'rxjs';
import { Review } from 'src/app/_models/review';
import { UserProfile } from 'src/app/_models/userProfile';
import { UserProfileService } from 'src/app/_services/user-profile/user-profile.service';

@Component({
  selector: 'app-index-page',
  templateUrl: './index-page.component.html',
  styleUrls: ['./index-page.component.scss']
})
export class IndexPageComponent implements OnInit {
  userProfile: UserProfile | undefined;

  constructor(private userProfileService: UserProfileService, private router: Router) { }

  ngOnInit(): void {
    this.userProfileService.getUserProfile().pipe(
      take(1)
    ).subscribe(
      (userProfile : any) => {
        this.userProfile = userProfile;
      });
  }

  /**
   * Navigate to the review page when a review is selected
   * @param review - The selected review to navigate to
   */
  onReviewSelected(review: Review) {
    this.router.navigate(['/review', review.id]);
  }
}
