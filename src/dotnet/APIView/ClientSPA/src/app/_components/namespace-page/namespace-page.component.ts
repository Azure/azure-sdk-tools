import { Component, Input, OnDestroy, OnInit } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Title } from '@angular/platform-browser';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MessageService } from 'primeng/api';
import { DialogModule } from 'primeng/dialog';
import { Subject, takeUntil, forkJoin, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { REVIEW_ID_ROUTE_PARAM, getQueryParams } from 'src/app/_helpers/router-helpers';
import { Review } from 'src/app/_models/review';
import { UserProfile } from 'src/app/_models/userProfile';
import { ReviewsService } from 'src/app/_services/reviews/reviews.service';
import { UserProfileService } from 'src/app/_services/user-profile/user-profile.service';
import { ProjectsService } from 'src/app/_services/projects/projects.service';
import { APIRevisionsService } from 'src/app/_services/revisions/revisions.service';
import { ReviewPageLayoutModule } from 'src/app/_modules/shared/review-page-layout.module';
import { ProjectNamespaceInfo, NamespaceDecisionStatus } from 'src/app/_models/namespaceModel';
import { RelatedReviewsResponse, RelatedReviewItem } from 'src/app/_models/projectModel';
import { canApproveForLanguage, canApproveForAnyLanguage } from 'src/app/_models/permissions';

interface NamespaceTableRow {
  language: string;
  namespace: string;
  status: NamespaceDecisionStatus;
  notes: string;
  decidedBy: string;
  review: RelatedReviewItem | null;
  latestRevisionId: string | null;
  canApprove: boolean;
}

@Component({
    selector: 'app-namespace-page',
    templateUrl: './namespace-page.component.html',
    styleUrls: ['./namespace-page.component.scss'],
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        RouterLink,
        DialogModule,
        ReviewPageLayoutModule,
    ]
})
export class NamespacePageComponent implements OnInit, OnDestroy {
  // Embedded mode inputs — when present, parent provides data and navigation uses query params
  @Input() embedded = false;
  @Input() embeddedReviewId: string | null = null;
  @Input() embeddedReview: Review | undefined;
  @Input() embeddedUserProfile: UserProfile | undefined;

  reviewId: string | null = null;
  review: Review | undefined = undefined;
  userProfile: UserProfile | undefined;
  
  projectId: string | null = null;
  projectName: string | null = null;
  relatedReviewsByLanguage: { [language: string]: RelatedReviewItem } = {};
  namespaceInfo: ProjectNamespaceInfo | null = null;
  
  tableRows: NamespaceTableRow[] = [];
  
  isLoading: boolean = true;
  loadFailed: boolean = false;

  // Feedback dialog state
  showFeedbackDialog: boolean = false;
  feedbackNotes: string = '';
  feedbackTargetRow: NamespaceTableRow | null = null;

  private destroy$ = new Subject<void>();

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private reviewsService: ReviewsService,
    private userProfileService: UserProfileService,
    private projectsService: ProjectsService,
    private apiRevisionsService: APIRevisionsService,
    private messageService: MessageService,
    private titleService: Title
  ) {}

  ngOnInit(): void {
    if (this.embedded) {
      this.reviewId = this.embeddedReviewId;
      this.review = this.embeddedReview;
      this.userProfile = this.embeddedUserProfile;
      this.loadNamespaceData();
    } else {
      this.reviewId = this.route.snapshot.paramMap.get(REVIEW_ID_ROUTE_PARAM);
      
      this.userProfileService
        .getUserProfile()
        .pipe(takeUntil(this.destroy$))
        .subscribe((userProfile: UserProfile) => {
          this.userProfile = userProfile;
          if (this.tableRows.length > 0) {
            this.buildTableRows();
          }
        });
      
      this.loadReview(this.reviewId!);
    }
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadReview(reviewId: string): void {
    this.reviewsService.getReview(reviewId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (review: Review) => {
          this.review = review;
          this.updatePageTitle();
          this.loadNamespaceData();
        },
        error: (err) => {
          this.loadFailed = true;
          this.isLoading = false;
        }
      });
  }

  loadNamespaceData(): void {
    if (!this.reviewId) {
      this.isLoading = false;
      this.loadFailed = true;
      return;
    }

    this.projectsService.getRelatedReviews(this.reviewId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response: RelatedReviewsResponse) => {
          this.projectId = response.projectId;
          this.projectName = response.projectName;
          this.relatedReviewsByLanguage = response.reviews;
          
          if (this.projectId) {
            this.loadNamespaceInfo();
          } else {
            this.buildTableRows();
            this.isLoading = false;
          }
        },
        error: (err) => {
          // If error is 404, it means no project is linked — show empty state instead of error
          if (err.status === 404) {
            this.projectId = null;
            this.buildTableRows();
            this.isLoading = false;
          } else {
            this.loadFailed = true;
            this.isLoading = false;
          }
        }
      });
  }

  loadNamespaceInfo(): void {
    if (!this.projectId) {
      this.buildTableRows();
      this.isLoading = false;
      return;
    }

    this.projectsService.getProjectNamespaces(this.projectId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (info: ProjectNamespaceInfo) => {
          this.namespaceInfo = info;
          this.buildTableRows();
          this.isLoading = false;
        },
        error: (err) => {
          // If error is 404, treat it as "no namespace info configured" and show empty state
          if (err && err.status === 404) {
            this.namespaceInfo = null;
            this.buildTableRows();
            this.isLoading = false;
          } else {
            // For other errors, mark load as failed so the user gets feedback
            this.loadFailed = true;
            this.isLoading = false;
          }
        }
      });
  }

  buildTableRows(): void {
    this.tableRows = [];
    
    if (!this.namespaceInfo?.currentNamespaceStatus) {
      return;
    }
    
    for (const lang of Object.keys(this.namespaceInfo.currentNamespaceStatus)) {
      const review = this.relatedReviewsByLanguage[lang] ?? null;
      const namespaceEntry = this.namespaceInfo.currentNamespaceStatus[lang];
      
      const canApprove = this.canUserApprove(lang);
      
      this.tableRows.push({
        language: lang,
        namespace: namespaceEntry.namespace || '',
        status: namespaceEntry.status,
        notes: namespaceEntry.notes || '',
        decidedBy: namespaceEntry.decidedBy || '',
        review,
        latestRevisionId: null,
        canApprove
      });
    }
    
    this.tableRows.sort((a, b) => a.language.localeCompare(b.language));
    
    // Fetch latest revision IDs for all reviews that have links
    this.fetchLatestRevisionIds();
  }
  
  private fetchLatestRevisionIds(): void {
    const rowsWithReviews = this.tableRows.filter(row => row.review !== null);
    if (rowsWithReviews.length === 0) {
      return;
    }
    
    // Fetch latest revision for each review in parallel
    const requests = rowsWithReviews.map(row => 
      this.apiRevisionsService.getLatestAPIRevision(row.review!.id).pipe(
        map(revision => ({ language: row.language, revisionId: revision?.id || null })),
        catchError(() => of({ language: row.language, revisionId: null }))
      )
    );
    
    forkJoin(requests)
      .pipe(takeUntil(this.destroy$))
      .subscribe(results => {
        for (const result of results) {
          console.log(`[Namespace] latestRevisionId for lang="${result.language}": ${result.revisionId}`);
          const row = this.tableRows.find(r => r.language === result.language);
          if (row) {
            row.latestRevisionId = result.revisionId;
          }
        }
      });
  }

  canUserApprove(language: string): boolean {
    return canApproveForLanguage(this.userProfile?.permissions, language);
  }

  canUserApproveAny(): boolean {
    return canApproveForAnyLanguage(this.userProfile?.permissions);
  }

  updatePageTitle(): void {
    if (this.review) {
      this.titleService.setTitle(`Namespace - ${this.review.packageName}`);
    }
  }

  getStatusClass(status: NamespaceDecisionStatus): string {
    const statusLower = status?.toString().toLowerCase();
    switch (statusLower) {
      case 'approved':
        return 'status-approved';
      case 'proposed':
        return 'status-proposed';
      case 'rejected':
        return 'status-rejected';
      case 'withdrawn':
        return 'status-withdrawn';
      default:
        return '';
    }
  }

  getStatusLabel(status: NamespaceDecisionStatus): string {
    const statusLower = status?.toString().toLowerCase();
    switch (statusLower) {
      case 'approved':
        return 'Approved';
      case 'proposed':
        return 'Proposed';
      case 'rejected':
        return 'Rejected';
      case 'withdrawn':
        return 'Withdrawn';
      default:
        return status;
    }
  }

  isApproved(row: NamespaceTableRow): boolean {
    return row.status?.toString().toLowerCase() === 'approved';
  }

  isProposed(row: NamespaceTableRow): boolean {
    return row.status?.toString().toLowerCase() === 'proposed';
  }

  isRejected(row: NamespaceTableRow): boolean {
    return row.status?.toString().toLowerCase() === 'rejected';
  }

  showApproveButton(row: NamespaceTableRow): boolean {
    const statusLower = row.status?.toString().toLowerCase();
    return row.canApprove && 
           !!row.namespace && // Must have a namespace to approve
           statusLower !== 'approved' && 
           statusLower !== 'withdrawn';
  }

  showFeedbackButton(row: NamespaceTableRow): boolean {
    const statusLower = row.status?.toString().toLowerCase();
    return row.canApprove && 
           !!row.namespace && // Must have a namespace to reject
           statusLower !== 'rejected' &&
           statusLower !== 'withdrawn';
  }

  approveNamespace(row: NamespaceTableRow): void {
    if (!this.projectId) return;
    
    this.projectsService.updateNamespaceStatus(this.projectId, row.language, NamespaceDecisionStatus.Approved, row.notes ?? '')
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: 'Namespace Approved',
            detail: `Namespace for ${row.language} has been approved.`
          });
          this.loadNamespaceInfo();
        },
        error: (err) => {
          this.messageService.add({
            severity: 'error',
            summary: 'Error',
            detail: err.error?.message || 'Failed to approve namespace.'
          });
        }
      });
  }

  sendFeedback(row: NamespaceTableRow): void {
    this.feedbackTargetRow = row;
    this.feedbackNotes = '';
    this.showFeedbackDialog = true;
  }

  submitFeedback(): void {
    if (!this.feedbackNotes.trim() || !this.projectId || !this.feedbackTargetRow) {
      this.messageService.add({
        severity: 'warn',
        summary: 'Required',
        detail: 'Please provide a rejection reason.'
      });
      return;
    }
    
    const row = this.feedbackTargetRow;
    this.projectsService.updateNamespaceStatus(this.projectId, row.language, NamespaceDecisionStatus.Rejected, this.feedbackNotes)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'warn',
            summary: 'Namespace Rejected',
            detail: `Namespace for ${row.language} has been rejected.`
          });
          this.closeFeedbackDialog();
          this.loadNamespaceInfo();
        },
        error: (err) => {
          this.messageService.add({
            severity: 'error',
            summary: 'Error',
            detail: err.error?.message || 'Failed to send feedback.'
          });
        }
      });
  }

  closeFeedbackDialog(): void {
    this.showFeedbackDialog = false;
    this.feedbackNotes = '';
    this.feedbackTargetRow = null;
  }

  navigateToReview(): void {
    const queryParams = getQueryParams(this.route);
    this.router.navigate(['/review', this.reviewId], { queryParams });
  }

  navigateToRevisions(): void {
    const queryParams = getQueryParams(this.route);
    this.router.navigate(['/revision', this.reviewId], { queryParams });
  }

  navigateToSamples(): void {
    const queryParams = getQueryParams(this.route);
    this.router.navigate(['/samples', this.reviewId], { queryParams });
  }

  navigateToConversations(): void {
    const queryParams = getQueryParams(this.route);
    this.router.navigate(['/conversation', this.reviewId], { queryParams });
  }

  getApprovedCount(): number {
    return this.tableRows.filter(r => r.status?.toString().toLowerCase() === 'approved').length;
  }

  getTotalCount(): number {
    return this.tableRows.length;
  }

  getProgressPercentage(): number {
    const total = this.getTotalCount();
    if (total === 0) return 0;
    return Math.round((this.getApprovedCount() / total) * 100);
  }
}
