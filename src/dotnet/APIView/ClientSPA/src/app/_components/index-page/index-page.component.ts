import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { Review, SelectItemModel } from 'src/app/_models/review';
import { UserProfile } from 'src/app/_models/userProfile';
import { UserProfileService } from 'src/app/_services/user-profile/user-profile.service';
import { ReviewsService } from 'src/app/_services/reviews/reviews.service';
import { getSupportedLanguages, usesTreeStyleParser } from 'src/app/_helpers/common-helpers';
import { MessageService } from 'primeng/api';

@Component({
    selector: 'app-index-page',
    templateUrl: './index-page.component.html',
    styleUrls: ['./index-page.component.scss'],
    standalone: false
})
export class IndexPageComponent implements OnInit {
  userProfile: UserProfile | undefined;
  showCreateReviewDialog = false;

  // Create Review form fields
  selectedLanguage: SelectItemModel | undefined;
  languages: SelectItemModel[] = [];
  packageNames: string[] = [];
  filteredPackageNames: string[] = [];
  selectedPackageName: string = '';
  reviewLabel: string = '';
  selectedFile: File | null = null;
  filePath: string = '';
  isCreating = false;

  constructor(
    private router: Router,
    private userProfileService: UserProfileService,
    private reviewsService: ReviewsService,
    private messageService: MessageService
  ) { }

  ngOnInit(): void {
    this.userProfileService.getUserProfile().subscribe({
      next: (profile: UserProfile) => {
        this.userProfile = profile;
      }
    });
    this.languages = getSupportedLanguages();
  }

  onReviewSelected(review: Review) {
    this.reviewsService.openReviewPage(review.id, review.language);
  }

  openCreateReviewDialog() {
    this.showCreateReviewDialog = true;
  }

  onLanguageChange() {
    if (this.selectedLanguage) {
      this.reviewsService.getPackageNames(this.selectedLanguage.data).subscribe({
        next: (names: string[]) => {
          this.packageNames = names;
        }
      });
    } else {
      this.packageNames = [];
    }
    this.selectedPackageName = '';
  }

  filterPackageNames(event: any) {
    const query = event.query?.toLowerCase() || '';
    this.filteredPackageNames = this.packageNames.filter(name =>
      name.toLowerCase().includes(query)
    );
  }

  onFileSelect(event: any) {
    this.selectedFile = event.files?.[0] || null;
  }

  onFileClear() {
    this.selectedFile = null;
  }

  createReview() {
    if (!this.selectedLanguage || !this.selectedFile) {
      return;
    }

    this.isCreating = true;
    const formData = new FormData();
    formData.append('language', this.selectedLanguage.data);
    formData.append('file', this.selectedFile);
    if (this.selectedPackageName) {
      formData.append('packageName', this.selectedPackageName);
    }
    if (this.filePath) {
      formData.append('filePath', this.filePath);
    }
    if (this.reviewLabel) {
      formData.append('label', this.reviewLabel);
    }

    this.reviewsService.createReview(formData).subscribe({
      next: (result) => {
        this.isCreating = false;
        this.showCreateReviewDialog = false;
        this.resetCreateForm();
        if (result?.reviewId) {
          this.router.navigate(['/review', result.reviewId]);
        }
      },
      error: () => {
        this.isCreating = false;
        this.messageService.add({ severity: 'error', summary: 'Error', detail: 'Failed to create review.' });
      }
    });
  }

  resetCreateForm() {
    this.selectedLanguage = undefined;
    this.selectedPackageName = '';
    this.reviewLabel = '';
    this.selectedFile = null;
    this.filePath = '';
    this.packageNames = [];
  }
}
