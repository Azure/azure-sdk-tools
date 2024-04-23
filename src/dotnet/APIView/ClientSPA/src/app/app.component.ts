import { ChangeDetectionStrategy, Component, HostBinding, OnInit } from '@angular/core';
import { AuthService } from './_services/auth/auth.service';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
export class AppComponent  implements OnInit{
  title : string = 'APIView';
  @HostBinding('class') appTheme : string = 'light-theme';

  constructor(private authService: AuthService) { }

  ngOnInit(): void {
    this.setAppTheme();
  }

  setAppTheme() {
    this.authService.getUserProfile().subscribe(
      (userProfile) => {
        this.appTheme = userProfile.preferences.theme
      });
  }
}
