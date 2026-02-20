import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpResponse } from '@angular/common/http';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { MessageService } from 'primeng/api';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { initializeTestBed } from '../../../test-setup';
import { Review } from 'src/app/_models/review';
import { NotificationsService } from 'src/app/_services/notifications/notifications.service';
import { APIRevisionsService } from 'src/app/_services/revisions/revisions.service';
import { ReviewsService } from 'src/app/_services/reviews/reviews.service';
import { SignalRService } from 'src/app/_services/signal-r/signal-r.service';
import { WorkerService } from 'src/app/_services/worker/worker.service';
import { createMockSignalRService, createMockNotificationsService, createMockWorkerService } from 'src/test-helpers/mock-services';
import { ReviewPageComponent } from './review-page.component';

// Mock ngx-ui-scroll to avoid vscroll dependency error
vi.mock('ngx-ui-scroll', () => {
  const UiScrollModuleMock = class UiScrollModule {
    static ɵmod = { 
      id: 'UiScrollModule',
      declarations: [],
      imports: [],
      exports: []
    };
    static ɵinj = { 
      imports: [],
      providers: []
    };
  };
  return {
    UiScrollModule: UiScrollModuleMock
  };
});

describe('ReviewPageComponent', () => {
  let component: ReviewPageComponent;
  let fixture: ComponentFixture<ReviewPageComponent>;
  let reviewsService: ReviewsService;
  let apiRevisionsService: APIRevisionsService;
  let httpMock: HttpTestingController;

  const mockNotificationsService = createMockNotificationsService();
  const mockSignalRService = createMockSignalRService();
  const mockWorkerService = createMockWorkerService();

  beforeAll(() => {
    initializeTestBed();
  });

  beforeEach(() => {
    TestBed.configureTestingModule({
      schemas: [NO_ERRORS_SCHEMA],
      declarations: [ReviewPageComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: NotificationsService, useValue: mockNotificationsService },
        { provide: SignalRService, useValue: mockSignalRService },
        { provide: WorkerService, useValue: mockWorkerService },
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
        MessageService
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

      vi.spyOn(reviewsService, 'getReviewContent').mockReturnValue(
        of(new HttpResponse<ArrayBuffer>({ status: 204 }))
      );
      component.loadReviewContent(reviewId, activeApiRevisionId, diffApiRevisionId);

      expect(component.loadFailed).toBeTruthy();
      expect(component.loadFailedMessage).toContain('API-Revision Content Not Found. The');
      expect(component.loadFailedMessage).toContain('active and/or diff API-Revision(s) may have been deleted.');
    });

    it('should set loadFailed and loadFailedMessage when Review is deleted', () => {
      var review = new Review();
      review.isDeleted = true;
      vi.spyOn(reviewsService, 'getReview').mockReturnValue(of(review));
      component.loadReview('testReviewId');

      expect(component.loadFailed).toBeTruthy();
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

    vi.spyOn(apiRevisionsService, 'getAPIRevisions').mockReturnValue(of({ result: [] }));
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
