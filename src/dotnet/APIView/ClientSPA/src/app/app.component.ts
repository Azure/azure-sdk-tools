import { Component, OnInit } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { UserProfileService } from './_services/user-profile/user-profile.service';
import { ConfigService } from './_services/config/config.service';
import { ScrollBarSize } from './_models/userPreferenceModel';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
export class AppComponent  implements OnInit{
  title : string = 'APIView';
  scrollBarHeight: string = '10px';
  scrollBarWidth: string = '10px';

  constructor(private userProfileService: UserProfileService, private configService: ConfigService, private titleService: Title) { }

  ngOnInit(): void {
    this.setAppTheme();
  }

  setAppTheme() {
    this.userProfileService.getUserProfile().subscribe(
      (userProfile) => {
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
        this.loadHighlightTheme(this.getHighlightTheme(theme));
      });
  }

  getHighlightTheme(appTheme: string): string {
    switch (appTheme) {
      case 'dark-theme':
        return 'atom-one-dark';
      case 'light-theme':
        return 'atom-one-light';
      case 'dark-solarized-theme':
        return 'monokai';
      default:
        return 'atom-one-light';
    }
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
}
