import { Component, OnInit } from '@angular/core';
import { UserProfile } from 'src/app/_models/auth_service_models';
import { AuthService } from 'src/app/_services/auth/auth.service';
import { environment } from 'src/environments/environment';

@Component({
  selector: 'app-nav-bar',
  templateUrl: './nav-bar.component.html',
  styleUrls: ['./nav-bar.component.scss']
})
export class NavBarComponent implements OnInit {
  userProfile : UserProfile | undefined;
  profilePageWebAppUrl : string | undefined
  logoutPageWebAppUrl : string  = environment.webAppUrl + "Account/Logout"
  assetsPath : string = environment.assetsPath;

  constructor(private authService: AuthService) { }

  ngOnInit(): void {
    this.authService.getUserProfile().subscribe(
      (userProfile : any) => {
        this.profilePageWebAppUrl = environment.webAppUrl + "Assemblies/profile/" + userProfile.userName;
      });
  }
}
