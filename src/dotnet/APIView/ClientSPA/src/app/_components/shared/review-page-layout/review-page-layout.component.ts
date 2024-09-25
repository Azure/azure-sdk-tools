import { Component, EventEmitter, Input, Output } from '@angular/core';
import { MenuItem } from 'primeng/api';
import { Review } from 'src/app/_models/review';
import { APIRevision } from 'src/app/_models/revision';
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
  @Input() apiRevisions: APIRevision[] = [];
  @Input() activeApiRevisionId: string | null = '';
  @Input() diffApiRevisionId: string | null = '';
  @Input() showPageoptionsButton: boolean = false;

  @Output() pageOptionsEmitter : EventEmitter<boolean> = new EventEmitter<boolean>();

  handlePageOptionsEmitter(showPageOptions: boolean) {
    this.pageOptionsEmitter.emit(showPageOptions);
  }
}
