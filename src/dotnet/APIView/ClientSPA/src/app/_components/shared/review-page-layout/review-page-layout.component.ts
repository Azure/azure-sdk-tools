import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NavBarComponent } from 'src/app/_components/shared/nav-bar/nav-bar.component';
import { ReviewInfoComponent } from 'src/app/_components/shared/review-info/review-info.component';
import { Review } from 'src/app/_models/review';
import { APIRevision } from 'src/app/_models/revision';
import { UserProfile } from 'src/app/_models/userProfile';

@Component({
    selector: 'app-review-page-layout',
    templateUrl: './review-page-layout.component.html',
    styleUrls: ['./review-page-layout.component.scss'],
    standalone: true,
    imports: [
        CommonModule,
        NavBarComponent,
        ReviewInfoComponent
    ]
})
export class ReviewPageLayoutComponent {
  @Input() review : Review | undefined = undefined;
  @Input() userProfile : UserProfile | undefined;
  @Input() apiRevisions: APIRevision[] = [];
  @Input() activeApiRevisionId: string | null = '';
  @Input() diffApiRevisionId: string | null = '';
  @Input() showPageoptionsButton: boolean = false;
  @Input() showLeftNavigation: boolean = true;
  @Input() activePage: 'reviews' | 'revisions' | 'samples' | 'conversations' = 'reviews';
  @Input() samplesRevisionCount: number = 0;
  @Input() conversationCount: number = 0;

  @Output() pageOptionsEmitter : EventEmitter<boolean> = new EventEmitter<boolean>();
  @Output() showLeftNavigationEmitter : EventEmitter<boolean> = new EventEmitter<boolean>();
  @Output() navigateToSamplesEmitter : EventEmitter<void> = new EventEmitter<void>();
  @Output() navigateToReviewsEmitter : EventEmitter<void> = new EventEmitter<void>();
  @Output() navigateToRevisionsEmitter : EventEmitter<void> = new EventEmitter<void>();
  @Output() navigateToConversationsEmitter : EventEmitter<void> = new EventEmitter<void>();

  handlePageOptionsEmitter(showPageOptions: boolean) {
    this.pageOptionsEmitter.emit(showPageOptions);
  }

  handleLeftNavigationEmitter(showLeftNavigation: boolean) {
    this.showLeftNavigationEmitter.emit(showLeftNavigation);
  }


}
