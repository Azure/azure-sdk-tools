import { Component, Input } from '@angular/core';
import { MenuItem } from 'primeng/api';
import { Review } from 'src/app/_models/review';
import { UserProfile } from 'src/app/_models/userProfile';

@Component({
  selector: 'app-review-page-layout',
  templateUrl: './review-page-layout.component.html',
  styleUrls: ['./review-page-layout.component.scss']
})
export class ReviewPageLayoutComponent {
  @Input() review : Review | undefined = undefined;
  @Input() userProfile : UserProfile | undefined;
  @Input() sideMenu: MenuItem[] | undefined;
}
