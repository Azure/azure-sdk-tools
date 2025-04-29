import { booleanAttribute, Component } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { take } from 'rxjs';
import { getSupportedLanguages } from 'src/app/_helpers/common-helpers';
import { USER_NAME_ROUTE_PARAM } from 'src/app/_helpers/router-helpers';
import { SelectItemModel } from 'src/app/_models/review';
import { ScrollBarSize } from 'src/app/_models/userPreferenceModel';
import { UserProfile } from 'src/app/_models/userProfile';
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
  userProfile : UserProfile | undefined;

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
  selectedScrollBarSize : ScrollBarSize | null = ScrollBarSize.Small;
  useSplitIndexPage : boolean = false;
  disableSaveButton : boolean = true;

  constructor(private route: ActivatedRoute, private userProfileService: UserProfileService) {}

  ngOnInit() {
    this.languages = getSupportedLanguages();
    this.userName = this.route.snapshot.paramMap.get(USER_NAME_ROUTE_PARAM);
    this.userProfileService.getUserProfile().subscribe(
      (userProfile : UserProfile) => {
        this.userProfile = userProfile;
        this.notificationEmail = userProfile.email;
        this.selectedLanguages = userProfile?.languages?.map((lang: string) => ({ label: lang, data: lang }));
        this.selectedTheme = this.themes.filter(t => t.data === userProfile.preferences.theme)[0];
        this.useSplitIndexPage = userProfile.preferences.useBetaIndexPage;
        this.selectedScrollBarSize = userProfile.preferences.scrollBarSize;
    });
  }

  saveProfileChanges() {
    this.disableSaveButton = true;
    this.userProfile!.email = this.notificationEmail!;
    this.userProfile!.languages = this.selectedLanguages.map((lang: SelectItemModel) => lang.data);
    this.userProfile!.preferences.theme = this.selectedTheme.data;
    this.userProfile!.preferences.useBetaIndexPage = this.useSplitIndexPage;
    this.userProfile!.preferences.scrollBarSize = this.selectedScrollBarSize;
    this.userProfileService.updateUserProfile(this.userProfile!).pipe(take(1)).subscribe({
      next: (response: any) => {
      },
      error: (error: any) => {
        this.disableSaveButton = false;
      }
    });
  }

  onProfileChange(event: any){
    this.disableSaveButton = false;
  }
}
