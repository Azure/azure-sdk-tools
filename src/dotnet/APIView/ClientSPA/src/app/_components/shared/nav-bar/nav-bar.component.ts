import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DrawerModule } from 'primeng/drawer';
import { SelectButtonModule } from 'primeng/selectbutton';
import { TimeagoModule } from 'ngx-timeago';
import { combineLatest, take } from 'rxjs';
import { REVIEW_ID_ROUTE_PARAM } from 'src/app/_helpers/router-helpers';
import { NotificationsFilter, SiteNotification } from 'src/app/_models/notificationsModel';
import { UserProfile } from 'src/app/_models/userProfile';
import { SelectItemModel } from 'src/app/_models/review';
import { AuthService } from 'src/app/_services/auth/auth.service';
import { ConfigService } from 'src/app/_services/config/config.service';
import { NotificationsService } from 'src/app/_services/notifications/notifications.service';
import { UserProfileService } from 'src/app/_services/user-profile/user-profile.service';
import { PermissionsService } from 'src/app/_services/permissions/permissions.service';
import { environment } from 'src/environments/environment';

@Component({
    selector: 'app-nav-bar',
    templateUrl: './nav-bar.component.html',
    styleUrls: ['./nav-bar.component.scss'],
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        RouterModule,
        DrawerModule,
        SelectButtonModule,
        TimeagoModule
    ]
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
  isAdmin: boolean = false;

  // Theme options
  themes : SelectItemModel[] = [
    { label: "Light", data: "light-theme" },
    { label: "Dark", data: "dark-theme" },
    { label: "Solarized", data: "dark-solarized-theme" }
  ];
  selectedTheme : SelectItemModel = { label: "Light", data: "light-theme" };

  constructor(private userProfileService: UserProfileService, private configService: ConfigService,
    private notificationsService: NotificationsService, private authService: AuthService, private route: ActivatedRoute,
    private http: HttpClient, private permissionsService: PermissionsService
  ) { }

  ngOnInit(): void {
    this.reviewId = this.route.snapshot.paramMap.get(REVIEW_ID_ROUTE_PARAM);

    // Use combineLatest to wait for both isLoggedIn and userProfile before checking approver status
    combineLatest([
      this.authService.isLoggedIn(),
      this.userProfileService.getUserProfile()
    ]).subscribe(([isLoggedIn, userProfile]) => {
      this.isLoggedIn = isLoggedIn;
      this.userProfile = userProfile;
      if (isLoggedIn && userProfile) {
        this.checkApproverStatus();
        this.checkAdminStatus(userProfile);
        // Initialize theme selection from user preferences
        const currentTheme = this.themes.find(t => t.data === userProfile.preferences.theme);
        if (currentTheme) {
          this.selectedTheme = currentTheme;
        }
      }
    });

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
    if (!this.userProfile || !this.isLoggedIn) {
      this.isApprover = false;
      return;
    }

    if (this.userProfile.permissions) {
      this.isApprover = this.permissionsService.isLanguageApprover(this.userProfile.permissions);
    } else {
      // Fallback: fetch permissions if not available in profile
      this.permissionsService.getMyPermissions().subscribe({
        next: (permissions) => {
          this.isApprover = this.permissionsService.isLanguageApprover(permissions);
        },
        error: () => {
          this.isApprover = false;
        }
      });
    }
  }

  private checkAdminStatus(userProfile: UserProfile) {
    if (userProfile.permissions) {
      this.isAdmin = this.permissionsService.isAdmin(userProfile.permissions);
    } else {
      this.permissionsService.getMyPermissions().subscribe({
        next: (permissions) => {
          this.isAdmin = this.permissionsService.isAdmin(permissions);
        },
        error: () => {
          this.isAdmin = false;
        }
      });
    }
  }

  changeTheme(theme: SelectItemModel) {
    this.selectedTheme = theme;
    if (this.userProfile) {
      this.userProfile.preferences.theme = theme.data;
      this.userProfileService.updateUserProfile(this.userProfile).pipe(take(1)).subscribe({
        next: () => {
          window.location.reload();
        },
        error: (error: any) => {
          console.error('Failed to update theme:', error);
        }
      });
    }
  }
}
