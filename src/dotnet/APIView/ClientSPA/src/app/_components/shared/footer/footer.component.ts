import { Component, OnInit } from '@angular/core';
import { AuthService } from '../../../_services/auth/auth.service';
import { Observable } from 'rxjs';

@Component({
  selector: 'app-footer',
  templateUrl: './footer.component.html',
  styleUrls: ['./footer.component.scss']
})

export class FooterComponent  implements OnInit {
  public currentYear: number = new Date().getFullYear();
  public appVersion : string = "";

  constructor(private authService: AuthService) { }

  ngOnInit(): void {
    this.getAppVersion();
  }

  getAppVersion() {
    this.authService.appVersion().subscribe({
      next: (response : any) => {
        this.appVersion = response.hash;
      }
    });
  }
}
