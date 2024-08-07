import { Component, Input, OnInit } from '@angular/core';
import { CookieService } from 'ngx-cookie-service';

@Component({
  selector: 'app-page-options-section',
  templateUrl: './page-options-section.component.html',
  styleUrls: ['./page-options-section.component.scss']
})
export class PageOptionsSectionComponent implements OnInit{
  @Input() sectionName : string = '';
  @Input() collapsedInput: boolean | undefined;
  sectionId: string | undefined;
  collapsed: boolean | undefined;
  sectionStateCookieKey : string | undefined;

  constructor(private cookieService: CookieService) { }

  ngOnInit() {
    this.sectionId = `${this.sectionName!.replace(/\s/g, '-').toLocaleLowerCase()}`;
    this.sectionStateCookieKey = `${this.sectionId}-is-collapsed`;

    if (this.cookieService.check(this.sectionStateCookieKey)) {
      this.collapsed = this.cookieService.get(this.sectionStateCookieKey).toLocaleLowerCase() === 'true';
    } else {
      if (this.collapsedInput) {
        this.cookieService.set(this.sectionStateCookieKey, this.collapsedInput!.toString());
        this.collapsed = this.collapsedInput!;
      }
    }
  }

  onCollapseChange(value: boolean) {
    this.collapsed = value;
    this.cookieService.set(this.sectionStateCookieKey!, this.collapsed.toString());
  }
}
