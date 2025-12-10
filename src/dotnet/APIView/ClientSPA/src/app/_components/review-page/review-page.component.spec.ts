import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ReviewPageComponent } from './review-page.component';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { NavBarComponent } from '../shared/nav-bar/nav-bar.component';
import { ReviewInfoComponent } from '../shared/review-info/review-info.component';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { CodePanelComponent } from '../code-panel/code-panel.component';
import { ReviewsListComponent } from '../reviews-list/reviews-list.component';
import { RevisionsListComponent } from '../revisions-list/revisions-list.component';
import { of } from 'rxjs';
import { ApprovalPipe } from 'src/app/_pipes/approval.pipe';
import { ReviewNavComponent } from '../review-nav/review-nav.component';
import { ReviewPageOptionsComponent } from '../review-page-options/review-page-options.component';
import { PageOptionsSectionComponent } from '../shared/page-options-section/page-options-section.component';
import { SharedAppModule } from 'src/app/_modules/shared/shared-app.module';
import { ReviewPageModule } from 'src/app/_modules/review-page.module';
import { MessageService } from 'primeng/api';
import { ReviewsService } from 'src/app/_services/reviews/reviews.service';
import { HttpResponse, provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import { APIRevisionsService } from 'src/app/_services/revisions/revisions.service';
import { Review } from 'src/app/_models/review';

describe('ReviewPageComponent', () => {
  let component: ReviewPageComponent;
  let fixture: ComponentFixture<ReviewPageComponent>;
  let reviewsService: ReviewsService;
  let apiRevisionsService: APIRevisionsService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
    declarations: [
        ReviewPageComponent,
        ReviewNavComponent,
        ReviewPageOptionsComponent,
        PageOptionsSectionComponent,
        NavBarComponent,
        ReviewInfoComponent,
        CodePanelComponent,
        ReviewsListComponent,
        RevisionsListComponent,
        ApprovalPipe
    ],
    imports: [BrowserAnimationsModule,
        SharedAppModule,
        ReviewPageModule],
    providers: [
        {
            provide: ActivatedRoute,
            useValue: {
                snapshot: {
                    paramMap: convertToParamMap({ reviewId: 'test' }),
                },
                queryParams: of(convertToParamMap({ activeApiRevisionId: 'test', diffApiRevisionId: 'test' }))
            },
        },
        APIRevisionsService,
        ReviewsService,
        MessageService,
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting()
    ]
});
    fixture = TestBed.createComponent(ReviewPageComponent);
    component = fixture.componentInstance;
    reviewsService = TestBed.inject(ReviewsService);
    apiRevisionsService = TestBed.inject(APIRevisionsService);
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('Set LoadFailed and loadFailedMessage', () => {
    it('should set loadFailed and loadFailedMessage when loadReviewContent returns status 204', () => {
      const reviewId = 'test-review-id';
      const activeApiRevisionId = 'test-active-api-revision-id';
      const diffApiRevisionId = 'test-diff-api-revision-id';
  
      spyOn(reviewsService, 'getReviewContent').and.returnValue(
        of(new HttpResponse<ArrayBuffer>({ status: 204 }))
      );
      component.loadReviewContent(reviewId, activeApiRevisionId, diffApiRevisionId);
  
      expect(component.loadFailed).toBeTrue();
      expect(component.loadFailedMessage).toContain('API-Revision Content Not Found. The');
      expect(component.loadFailedMessage).toContain('active and/or diff API-Revision(s) may have been deleted.');
    });

    it('should set loadFailed and loadFailedMessage when Review is deleted', () => {
      var review = new Review();
      review.isDeleted = true;
      spyOn(reviewsService, 'getReview').and.returnValue(of(review));
      component.loadReview('testReviewId');
  
      expect(component.loadFailed).toBeTrue();
      expect(component.loadFailedMessage).toContain('Review has been deleted.');
    });
  });

  it('should include activeAPIRevision and diffAPIRevision in pageRevisions of the getAPIRevisions call when they are present', () => {
    const activeApiRevisionId = 'active-revision-id';
    const diffApiRevisionId = 'diff-revision-id';
    const reviewId = 'test-review-id';

    component.activeApiRevisionId = activeApiRevisionId;
    component.diffApiRevisionId = diffApiRevisionId;
    component.reviewId = reviewId;

    spyOn(apiRevisionsService, 'getAPIRevisions').and.returnValue(of({ result: [] }));
    component.loadAPIRevisions(0, component.apiRevisionPageSize);
    expect(apiRevisionsService.getAPIRevisions).toHaveBeenCalledWith(
      0,
      component.apiRevisionPageSize,
      reviewId,
      undefined,
      undefined,
      undefined,
      'createdOn',
      undefined,
      undefined,
      undefined,
      true,
      [activeApiRevisionId, diffApiRevisionId]
    );
  });
});