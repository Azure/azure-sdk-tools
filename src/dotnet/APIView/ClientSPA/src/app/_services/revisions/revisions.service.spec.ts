import { TestBed } from '@angular/core/testing';

import { RevisionsService } from './revisions.service';
import { HttpClientTestingModule } from '@angular/common/http/testing';

describe('RevisionsService', () => {
  let service: RevisionsService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule]
    });
    service = TestBed.inject(RevisionsService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
