import { TestBed } from '@angular/core/testing';

import { CommentsService } from './comments.service';
import { HttpClientTestingModule } from '@angular/common/http/testing';

describe('CommentsService', () => {
  let service: CommentsService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
    });
    service = TestBed.inject(CommentsService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
