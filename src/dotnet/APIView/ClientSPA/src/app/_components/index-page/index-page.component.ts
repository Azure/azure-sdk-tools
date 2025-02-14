import { Component } from '@angular/core';
import { take } from 'rxjs';
import { Review } from 'src/app/_models/review';
import { UserProfile } from 'src/app/_models/userProfile';
import { UserProfileService } from 'src/app/_services/user-profile/user-profile.service';

@Component({
  selector: 'app-index-page',
  templateUrl: './index-page.component.html',
  styleUrls: ['./index-page.component.scss']
})
export class IndexPageComponent {
  review : Review | undefined = undefined;
  userProfile: UserProfile | undefined;

  constructor(private userProfileService: UserProfileService) { }

  ngOnInit(): void {
    this.userProfileService.getUserProfile().pipe(
      take(1)
    ).subscribe(
      (userProfile : any) => {
        this.userProfile = userProfile;
      });
  }

  /**
   * Pass ReviewId to revision component to load revisions
   *  * @param reviewId
   */
  getRevisions(review: Review) {
    this.review = review;
  }
}
