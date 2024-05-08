import { Component, HostBinding, OnInit } from '@angular/core';
import { AuthService } from './_services/auth/auth.service';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
export class AppComponent  implements OnInit{
  title : string = 'APIView';

  constructor(private authService: AuthService) { }

  ngOnInit(): void {
    this.setAppTheme();
  }

  setAppTheme() {
    this.authService.getUserProfile().subscribe(
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
