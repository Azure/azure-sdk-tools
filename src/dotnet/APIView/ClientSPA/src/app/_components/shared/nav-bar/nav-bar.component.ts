import { Component, OnInit } from '@angular/core';
import { UserProfile } from 'src/app/_models/userProfile';
import { ConfigService } from 'src/app/_services/config/config.service';
import { UserProfileService } from 'src/app/_services/user-profile/user-profile.service';
import { environment } from 'src/environments/environment';

@Component({
  selector: 'app-nav-bar',
  templateUrl: './nav-bar.component.html',
  styleUrls: ['./nav-bar.component.scss']
})
export class NavBarComponent implements OnInit {
  userProfile : UserProfile | undefined;
  profilePageWebAppUrl : string | undefined
  logoutPageWebAppUrl : string  = this.configService.webAppUrl + "Account/Logout"
  assetsPath : string = environment.assetsPath;

  constructor(private userProfileService: UserProfileService, private configService: ConfigService) { }

  ngOnInit(): void {
    this.userProfileService.getUserProfile().subscribe(
      (userProfile : any) => {
        this.profilePageWebAppUrl = this.configService.webAppUrl + "Assemblies/profile/" + userProfile.userName;
      });
  }
}
