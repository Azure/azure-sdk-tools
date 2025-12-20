import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MenuItem } from 'primeng/api';
import { MenuModule } from 'primeng/menu';
import { RippleModule } from 'primeng/ripple';
import { TooltipModule } from 'primeng/tooltip';
import { BadgeModule } from 'primeng/badge';
import { NavBarComponent } from 'src/app/_components/shared/nav-bar/nav-bar.component';
import { ReviewInfoComponent } from 'src/app/_components/shared/review-info/review-info.component';
import { Review } from 'src/app/_models/review';
import { APIRevision } from 'src/app/_models/revision';
import { SamplesRevision } from 'src/app/_models/samples';
import { UserProfile } from 'src/app/_models/userProfile';

@Component({
    selector: 'app-review-page-layout',
    templateUrl: './review-page-layout.component.html',
    styleUrls: ['./review-page-layout.component.scss'],
    standalone: true,
    imports: [
        CommonModule,
        MenuModule,
        RippleModule,
        TooltipModule,
        BadgeModule,
        NavBarComponent,
        ReviewInfoComponent
    ]
})
export class ReviewPageLayoutComponent {
  @Input() review : Review | undefined = undefined;
  @Input() userProfile : UserProfile | undefined;
  @Input() sideMenu: MenuItem[] | undefined;
  @Input() apiRevisions: APIRevision[] = [];
  @Input() samplesRevisions: SamplesRevision[] = [];
  @Input() activeApiRevisionId: string | null = '';
  @Input() activeSamplesRevisionId: string | null = '';
  @Input() diffApiRevisionId: string | null = '';
  @Input() showPageoptionsButton: boolean = false;

  @Output() pageOptionsEmitter : EventEmitter<boolean> = new EventEmitter<boolean>();

  handlePageOptionsEmitter(showPageOptions: boolean) {
    this.pageOptionsEmitter.emit(showPageOptions);
  }
}
