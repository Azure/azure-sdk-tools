import { Component, OnInit } from '@angular/core';
import { SiteNotification } from 'src/app/_models/notificationsModel';
import { UserProfile } from 'src/app/_models/userProfile';
import { AuthService } from 'src/app/_services/auth/auth.service';
import { ConfigService } from 'src/app/_services/config/config.service';
import { NotificationsService } from 'src/app/_services/notifications/notifications.service';
import { UserProfileService } from 'src/app/_services/user-profile/user-profile.service';
import { environment } from 'src/environments/environment';

@Component({
  selector: 'app-nav-bar',
  templateUrl: './nav-bar.component.html',
  styleUrls: ['./nav-bar.component.scss']
})
export class NavBarComponent implements OnInit {
  userProfile : UserProfile | undefined;
  logoutPageWebAppUrl : string  = this.configService.webAppUrl + "Account/Logout"
  assetsPath : string = environment.assetsPath;
  notificationsSidePanel : boolean | undefined = undefined;
  notifications: SiteNotification[] = [];
  isLoggedIn: boolean = false;

  constructor(private userProfileService: UserProfileService, private configService: ConfigService,
    private notificationsService: NotificationsService, private authService: AuthService
  ) { }

  ngOnInit(): void {
    this.authService.isLoggedIn().subscribe(isLoggedIn => {
      this.isLoggedIn = isLoggedIn;
    });

    this.userProfileService.getUserProfile().subscribe(
      (userProfile : any) => {
        this.userProfile = userProfile;
      }
    );

    this.notificationsService.notifications$.subscribe(notifications => {
      this.notifications = notifications;
    });
  }

  clearNotification(id: string) {
    console.log("Clearing notification with id: " + id);
    this.notificationsService.clearNotification(id);
  }
}
