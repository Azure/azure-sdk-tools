import { TestBed } from '@angular/core/testing';

import { RevisionsService } from './revisions.service';

describe('RevisionsService', () => {
  let service: RevisionsService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(RevisionsService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
