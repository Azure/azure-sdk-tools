import { Component, OnInit } from '@angular/core';
import { UserProfileService } from './_services/user-profile/user-profile.service';
import { ConfigService } from './_services/config/config.service';
import { ScrollBarSize } from './_models/userPreferenceModel';
import { Subject, takeUntil } from 'rxjs';
import { AIReviewJobCompletedDto } from './_dtos/aiReviewJobCompletedDto';
import { UserProfile } from './_models/userProfile';
import { SiteNotification } from './_models/notificationsModel';
import { SignalRService } from './_services/signal-r/signal-r.service';
import { getAIReviewNotifiationInfo } from './_helpers/common-helpers';
import { NotificationsService } from './_services/notifications/notifications.service';
import { ThemeHelper } from './_helpers/theme.helper';

@Component({
    selector: 'app-root',
    templateUrl: './app.component.html',
    styleUrls: ['./app.component.scss'],
    standalone: false
})
export class AppComponent  implements OnInit{
  title : string = 'APIView';
  scrollBarHeight: string = '10px';
  scrollBarWidth: string = '10px';
  userProfile: UserProfile | undefined = undefined;

  private destroy$ = new Subject<void>();
  
  constructor(private userProfileService: UserProfileService, private configService: ConfigService, 
    private notificationsService: NotificationsService, private signalRService: SignalRService) { }

  ngOnInit(): void {
    this.setAppTheme();
    this.handleRealTimeAIReviewUpdates();
  }

  setAppTheme() {
    this.userProfileService.getUserProfile().subscribe({
      next: (userProfile) => {
        this.userProfile = userProfile;
        const theme = userProfile.preferences.theme;
        switch (userProfile.preferences.scrollBarSize) {
          case ScrollBarSize.Medium:
            this.scrollBarHeight = this.scrollBarWidth = '15px';
            break;
          case ScrollBarSize.Large:
            this.scrollBarHeight = this.scrollBarWidth = '20px';
            break;
          default:
            this.scrollBarHeight = this.scrollBarWidth = '10px';
        }

        this.configService.setAppTheme(theme);
        
        const body = document.body;
        if (theme !== "light-theme") {
          body.classList.remove("light-theme");
          body.classList.add(theme);
        }
        this.loadHighlightTheme(ThemeHelper.getHighlightTheme(theme));
      },
      error: () => {
        // If user profile fails (e.g., 403 Forbidden), use default theme
        this.configService.setAppTheme('light-theme');
      }
    });
  }

  // Load the highlight.js theme dynamically
  loadHighlightTheme(theme: string): void {
    const existingLinkElement = document.getElementById('highlight-theme');
    if (existingLinkElement) {
      existingLinkElement.remove();
    }
    const linkElement = document.createElement('link');
    linkElement.rel = 'stylesheet';
    linkElement.href = `https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.3.1/styles/${theme}.min.css`;
    linkElement.id = 'highlight-theme';
    document.head.appendChild(linkElement);
  }

  reloadPage(): void {
    window.location.reload();
  }

  handleRealTimeAIReviewUpdates() {
    this.signalRService.onAIReviewUpdates().pipe(takeUntil(this.destroy$)).subscribe({
      next: (aiReviewUpdate: AIReviewJobCompletedDto) => {
        if (aiReviewUpdate.createdBy == this.userProfile?.userName) {
          const notificationInfo = getAIReviewNotifiationInfo(aiReviewUpdate, window.location.origin);
          if (notificationInfo) {
            this.notificationsService.addNotification(notificationInfo[0]);
          }
        }
      }
    });
  }
}
