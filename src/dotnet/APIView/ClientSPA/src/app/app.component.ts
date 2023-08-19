import { Component, HostBinding, OnInit } from '@angular/core';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss'],
})
export class AppComponent  implements OnInit{
  title : string = 'APIView';
  @HostBinding('class') appTheme : string = 'light-theme';

  ngOnInit(): void {
    this.setAppTheme();
  }

  setAppTheme() {
    this.appTheme = 'dark-solarized-theme'
  }
}
