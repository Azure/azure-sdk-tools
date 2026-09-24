import { TestBed } from '@angular/core/testing';
import { initializeTestBed } from '../../../test-setup';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { CommentsService } from './comments.service';
import { ConfigService } from '../config/config.service';

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
