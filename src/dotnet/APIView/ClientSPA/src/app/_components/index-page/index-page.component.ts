import { Component, OnInit } from '@angular/core';
import { ConfigService } from 'src/app/_services/config/config.service';

@Component({
  selector: 'app-index-page',
  templateUrl: './index-page.component.html',
  styleUrls: ['./index-page.component.scss']
})
export class IndexPageComponent implements OnInit {

  constructor(private configService: ConfigService) { }

  ngOnInit(): void {
    // Redirect to the classic ASP.NET index page
    window.location.href = this.configService.webAppUrl;
  }
}
