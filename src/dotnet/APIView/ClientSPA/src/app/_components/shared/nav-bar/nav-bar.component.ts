import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { REVIEW_ID_ROUTE_PARAM } from 'src/app/_helpers/router-helpers';
import { NotificationsFilter, SiteNotification } from 'src/app/_models/notificationsModel';
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
  notificationsFilters: any[] = [
    { label: 'All', value: NotificationsFilter.All },
    { label: 'Page', value: NotificationsFilter.Page },
  ];
  notificationsFilter : NotificationsFilter = NotificationsFilter.All;
  isLoggedIn: boolean = false;
  reviewId: string | null = null;

  constructor(private userProfileService: UserProfileService, private configService: ConfigService,
    private notificationsService: NotificationsService, private authService: AuthService,
    private router: Router, private route: ActivatedRoute
  ) { }

  ngOnInit(): void {
    this.reviewId = this.route.snapshot.paramMap.get(REVIEW_ID_ROUTE_PARAM);
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

  updateNotificationsFilter(event : any) {
    this.notificationsFilter = event.value;
  }

  filteredNotifications() : SiteNotification [] {
    if (this.notificationsFilter === NotificationsFilter.All) {
      return this.notifications;
    }
    if (this.notificationsFilter === NotificationsFilter.Page && this.reviewId) {
      return this.notifications.filter(n => n.reviewId === this.reviewId);
    }
    return this.notifications;
  }

  clearNotification(id: string) {
    this.notificationsService.clearNotification(id);
  }

  clearAllNotification() {
    this.notificationsService.clearAll();
  }
}
