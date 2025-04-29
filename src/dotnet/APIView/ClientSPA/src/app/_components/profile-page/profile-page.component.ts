import { Component } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { getSupportedLanguages } from 'src/app/_helpers/common-helpers';
import { USER_NAME_ROUTE_PARAM } from 'src/app/_helpers/router-helpers';
import { SelectItemModel } from 'src/app/_models/review';
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
  languages: SelectItemModel[] = [];
  selectedLanguages: SelectItemModel[] = [];
  themes : string[] = ["light-theme", "dark-theme", "dark-solarized-theme"]; 

  constructor(private route: ActivatedRoute, private userProfileService: UserProfileService) {}

  ngOnInit() {
    this.languages = getSupportedLanguages();
    this.userName = this.route.snapshot.paramMap.get(USER_NAME_ROUTE_PARAM);
    this.userProfileService.getUserProfile().subscribe(
      (userProfile : any) => {
        this.userProfile = userProfile;
    });
  }

}
