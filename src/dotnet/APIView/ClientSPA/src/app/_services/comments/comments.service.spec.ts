import { TestBed } from '@angular/core/testing';
import { initializeTestBed } from '../../../test-setup';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { CommentsService } from './comments.service';
import { ConfigService } from '../config/config.service';
import { CommentType, CommentSeverity } from 'src/app/_models/commentItemModel';

describe('CommentsService', () => {
  let service: CommentsService;
  let httpMock: HttpTestingController;
  let configService: ConfigService;

  beforeAll(() => {
    initializeTestBed();
  });

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        CommentsService,
        {
          provide: ConfigService,
          useValue: {
            apiUrl: 'https://localhost:5001/api/'
          }
        }
      ]
    });
    service = TestBed.inject(CommentsService);
    httpMock = TestBed.inject(HttpTestingController);
    configService = TestBed.inject(ConfigService);
  });

  afterEach(() => {
    httpMock.verify(); 
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  describe('createComment', () => {
    const reviewId = 'review-1';
    const revisionId = 'revision-1';
    const elementId = 'elem-1';
    const commentText = 'Hello world';

    it('should include apiVersionId in form data when provided', () => {
      service.createComment(reviewId, revisionId, elementId, commentText, CommentType.APIRevision,
        false, null, undefined, 'v2026-01-01').subscribe();

      const req = httpMock.expectOne(`${configService.apiUrl}comments`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body.get('apiVersionId')).toBe('v2026-01-01');
      req.flush({});
    });

    it('should NOT include apiVersionId in form data when not provided', () => {
      service.createComment(reviewId, revisionId, elementId, commentText, CommentType.APIRevision).subscribe();

      const req = httpMock.expectOne(`${configService.apiUrl}comments`);
      expect(req.request.body.has('apiVersionId')).toBe(false);
      req.flush({});
    });

    it('should NOT include apiVersionId in form data when null', () => {
      service.createComment(reviewId, revisionId, elementId, commentText, CommentType.APIRevision,
        false, null, undefined, null).subscribe();

      const req = httpMock.expectOne(`${configService.apiUrl}comments`);
      expect(req.request.body.has('apiVersionId')).toBe(false);
      req.flush({});
    });
  });

  it('should send a PATCH request to update a comment', () => {
    const reviewId = '123';
    const commentId = '456';
    const commentText = 'Updated comment text';

    service.updateComment(reviewId, commentId, commentText).subscribe(response => {
      expect(response).toBeTruthy();
    });

    const req = httpMock.expectOne(`${configService.apiUrl}comments/${reviewId}/${commentId}/updateCommentText`);
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body.get('commentText')).toBe(commentText);
    expect(req.request.withCredentials).toBe(true);
    expect(req.request.headers.has('Content-Type')).toBe(false);
    req.flush({}); // Mock response
  });
});
