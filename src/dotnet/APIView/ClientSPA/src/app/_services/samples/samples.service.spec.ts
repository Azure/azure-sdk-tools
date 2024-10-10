import { TestBed } from '@angular/core/testing';

import { SamplesRevisionService } from './samples.service';
import { HttpClientTestingModule } from '@angular/common/http/testing';

describe('SamplesService', () => {
  let service: SamplesRevisionService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
    });
    service = TestBed.inject(SamplesRevisionService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
