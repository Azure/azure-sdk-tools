import { Component, HostBinding, OnInit } from '@angular/core';
import { UserProfileService } from './_services/user-profile/user-profile.service';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
export class AppComponent  implements OnInit{
  title : string = 'APIView';

  constructor(private userProfileService: UserProfileService) { }

  ngOnInit(): void {
    this.setAppTheme();
  }

  setAppTheme() {
    this.userProfileService.getUserProfile().subscribe(
      (userProfile) => {
        const theme = userProfile.preferences.theme;
        const body = document.body;
        if (theme !== "light-theme") {
          body.classList.remove("light-theme");
          body.classList.add(theme);
        }
      });
  }
}
