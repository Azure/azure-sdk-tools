import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { HttpClient } from '@angular/common/http';
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
  RequestReviewPageUrl: string = this.configService.webAppUrl + "Assemblies/RequestedReviews"
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
  isApprover: boolean = false;

  constructor(private userProfileService: UserProfileService, private configService: ConfigService,
    private notificationsService: NotificationsService, private authService: AuthService, private route: ActivatedRoute,
    private http: HttpClient
  ) { }

  ngOnInit(): void {
    this.reviewId = this.route.snapshot.paramMap.get(REVIEW_ID_ROUTE_PARAM);
    this.authService.isLoggedIn().subscribe(isLoggedIn => {
      this.isLoggedIn = isLoggedIn;
    });

    this.userProfileService.getUserProfile().subscribe(
      (userProfile : any) => {
        this.userProfile = userProfile;
        // Check if user is an approver
        this.checkApproverStatus();
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

  clearNotification(key: string) {
    this.notificationsService.clearNotification(key);
  }

  clearAllNotification() {
    this.notificationsService.clearAll();
  }

  private checkApproverStatus() {
    if (this.userProfile?.userName) {
      // Call the API to get allowed approvers
      this.http.get<string>(`${this.configService.apiUrl}/Reviews/allowedApprovers`).subscribe({
        next: (allowedApprovers) => {
          if (allowedApprovers) {
            // Split comma-separated string and check if current user is in the list
            const approversList = allowedApprovers.split(',').map(username => username.trim());
            this.isApprover = approversList.includes(this.userProfile?.userName || '');
          }
        },
        error: (error) => {
          console.error('Failed to fetch allowed approvers:', error);
          this.isApprover = false;
          // Optionally, notify the user here if desired
        }
      });
    }
  }
}
