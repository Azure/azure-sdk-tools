import { TestBed } from '@angular/core/testing';

import { CommentsService } from './comments.service';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ConfigService } from '../config/config.service';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';

describe('CommentsService', () => {
  let service: CommentsService;
  let httpMock: HttpTestingController;
  let configService: ConfigService;

  beforeEach(() => {
    TestBed.configureTestingModule({
    imports: [],
    providers: [
        CommentsService,
        {
            provide: ConfigService,
            useValue: {
                apiUrl: 'https://localhost:5001/api/'
            }
        },
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting()
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
    expect(req.request.withCredentials).toBeTrue();
    expect(req.request.headers.has('Content-Type')).toBeFalse();
    req.flush({}); // Mock response
  });
});
