import { Component } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { take } from 'rxjs';
import { getSupportedLanguages } from 'src/app/_helpers/common-helpers';
import { USER_NAME_ROUTE_PARAM } from 'src/app/_helpers/router-helpers';
import { SelectItemModel } from 'src/app/_models/review';
import { ScrollBarSize } from 'src/app/_models/userPreferenceModel';
import { UserProfile } from 'src/app/_models/userProfile';
import { ReviewsService } from 'src/app/_services/reviews/reviews.service';
import { UserProfileService } from 'src/app/_services/user-profile/user-profile.service';
import { environment } from 'src/environments/environment';

@Component({
  selector: 'app-profile-page',
  templateUrl: './profile-page.component.html',
  styleUrl: './profile-page.component.scss'
})
export class ProfilePageComponent {
  assetsPath : string = environment.assetsPath;
  userName : string | null = null;
  userEmail : string | undefined = undefined;
  userProfile : UserProfile | undefined;
  allowedApprovers : string[] = [];

  notificationEmail: string | null = null;
  languages: SelectItemModel[] = [];
  selectedLanguages: SelectItemModel[] = [];
  themes : SelectItemModel[] = [
    { label: "light", data: "light-theme" },
    { label: "dark", data: "dark-theme" },
    { label: "dark-solarized", data: "dark-solarized-theme" }
  ];
  selectedTheme : SelectItemModel = { label: "light", data: "light-theme" };
  scrollBarSizes : string[] = ["small", "medium", "large"];
  selectedScrollBarSize : ScrollBarSize = ScrollBarSize.Small;
  disableSaveButton : boolean = true;
  isLoaded: boolean | undefined = undefined;

  constructor(private route: ActivatedRoute, private userProfileService: UserProfileService,
    private reviewsService: ReviewsService) {}

  ngOnInit() {
    this.languages = getSupportedLanguages();
    this.userName = this.route.snapshot.paramMap.get(USER_NAME_ROUTE_PARAM);
    if (this.userName) {
      this.userProfileService.getUserProfile().subscribe({
        next: (userProfile : UserProfile) => {
          this.userProfile = userProfile;
          this.notificationEmail = userProfile.email;
          this.selectedLanguages = userProfile?.preferences.approvedLanguages?.map((lang: string) => ({ label: lang, data: lang }));
          console.log(userProfile);
          console.log(this.selectedLanguages);
          this.selectedTheme = this.themes.filter(t => t.data === userProfile.preferences.theme)[0];
          this.selectedScrollBarSize = userProfile.preferences.scrollBarSize;

          if (this.userName !== userProfile.userName) {
            this.userProfileService.getUserProfile(this.userName!).subscribe({
              next: (userProfile: UserProfile) => {
                this.userEmail = userProfile.email;
                this.isLoaded = true;
              },
              error: (error: any) => {
                this.isLoaded = false;
              }
            });
          } else {
            this.isLoaded = true;
          }
        },
        error: (error: any) => {
          this.isLoaded = false;
        }
      });
      this.reviewsService.getAllowedApprovers().subscribe({
        next: (allowedApprovers: string[]) => {
          this.allowedApprovers = allowedApprovers;
        }
      });
    }
  }

  saveProfileChanges() {
    this.disableSaveButton = true;
    this.userProfile!.email = this.notificationEmail!;
    this.userProfile!.preferences.approvedLanguages = this.selectedLanguages.map((lang: SelectItemModel) => lang.data);
    this.userProfile!.preferences.theme = this.selectedTheme.data;
    this.userProfile!.preferences.scrollBarSize = this.selectedScrollBarSize;
    this.userProfileService.updateUserProfile(this.userProfile!).pipe(take(1)).subscribe({
      next: (response: any) => {
        window.location.reload();
      },
      error: (error: any) => {
        this.disableSaveButton = false;
      }
    });
  }

  onProfileChange(event: any){
    if (event !== null && event !== undefined) {
      // Update the model for input fields
      if (typeof event === 'string') {
        this.notificationEmail = event;
      }
      // Update the model for multiselect changes
      if (event.value !== undefined) {
        this.selectedLanguages = event.value;
      }
    }
    this.disableSaveButton = false;
  }
}
