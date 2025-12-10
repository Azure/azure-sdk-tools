import { TestBed } from '@angular/core/testing';

import { SamplesRevisionService } from './samples.service';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';

describe('SamplesService', () => {
  let service: SamplesRevisionService;

  beforeEach(() => {
    TestBed.configureTestingModule({
    imports: [],
    providers: [provideHttpClient(withInterceptorsFromDi()), provideHttpClientTesting()]
});
    service = TestBed.inject(SamplesRevisionService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
