import { Component, EventEmitter, Input, OnDestroy, Output, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subject, takeUntil } from 'rxjs';
import { NavBarComponent } from 'src/app/_components/shared/nav-bar/nav-bar.component';
import { ReviewInfoComponent } from 'src/app/_components/shared/review-info/review-info.component';
import { Review } from 'src/app/_models/review';
import { APIRevision } from 'src/app/_models/revision';
import { UserProfile } from 'src/app/_models/userProfile';
import { isAdmin } from 'src/app/_models/permissions';
import { ProjectsService } from 'src/app/_services/projects/projects.service';

@Component({
    selector: 'app-review-page-layout',
    templateUrl: './review-page-layout.component.html',
    styleUrls: ['./review-page-layout.component.scss'],
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        NavBarComponent,
        ReviewInfoComponent
    ]
})
export class ReviewPageLayoutComponent implements OnDestroy {
  @Input() review : Review | undefined = undefined;
  @Input() userProfile : UserProfile | undefined;
  @Input() apiRevisions: APIRevision[] = [];
  @Input() activeApiRevisionId: string | null = '';
  @Input() diffApiRevisionId: string | null = '';
  @Input() showPageoptionsButton: boolean = false;
  @Input() showLeftNavigation: boolean = true;
  @Input() activePage: 'reviews' | 'revisions' | 'samples' | 'conversations' | 'namespace' = 'reviews';
  @Input() samplesRevisionCount: number = 0;
  @Input() conversationCount: number = 0;

  namespaceStatus: string | null = null;
  private destroy$ = new Subject<void>();

  @Output() pageOptionsEmitter : EventEmitter<boolean> = new EventEmitter<boolean>();
  @Output() showLeftNavigationEmitter : EventEmitter<boolean> = new EventEmitter<boolean>();
  @Output() navigateToSamplesEmitter : EventEmitter<void> = new EventEmitter<void>();
  @Output() navigateToReviewsEmitter : EventEmitter<void> = new EventEmitter<void>();
  @Output() navigateToRevisionsEmitter : EventEmitter<void> = new EventEmitter<void>();
  @Output() navigateToConversationsEmitter : EventEmitter<void> = new EventEmitter<void>();
  @Output() navigateToNamespaceEmitter : EventEmitter<void> = new EventEmitter<void>();

  showPageOptions: boolean = true;

  constructor(private projectsService: ProjectsService) {}

  get showNamespaceTab(): boolean {
    return isAdmin(this.userProfile?.permissions) && !!this.review?.projectId;
  }

  get showNamespaceAlert(): boolean {
    return this.showNamespaceTab && this.namespaceStatus === 'Proposed';
  }

  handlePageOptionsEmitter(showPageOptions: boolean) {
    this.pageOptionsEmitter.emit(showPageOptions);
  }

  handleLeftNavigationEmitter(showLeftNavigation: boolean) {
    this.showLeftNavigationEmitter.emit(showLeftNavigation);
  }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['review'] && this.review?.id) {
      this.projectsService.getNamespaceStatus(this.review.id)
        .pipe(takeUntil(this.destroy$))
        .subscribe({ next: (r) => { this.namespaceStatus = r.status; }, error: () => { this.namespaceStatus = null; } });
    }
    if (changes['userProfile'] && this.userProfile) {
      this.showPageOptions = this.userProfile.preferences?.hideReviewPageOptions != undefined
        ? !this.userProfile.preferences.hideReviewPageOptions
        : false;
    }
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }

  onRightPanelCheckChange(event: any) {
    this.showPageOptions = event.target.checked;
    this.pageOptionsEmitter.emit(event.target.checked);
  }

  onLeftPanelCheckChange(event: any) {
    this.showLeftNavigation = event.target.checked;
    this.showLeftNavigationEmitter.emit(event.target.checked);
  }
}
