import { TestBed } from '@angular/core/testing';

import { HttpErrorInterceptorService } from './http-error-interceptor.service';
import { HttpClientTestingModule } from '@angular/common/http/testing';

describe('HttpErrorInterceptorService', () => {
  let service: HttpErrorInterceptorService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [
        HttpErrorInterceptorService
      ]
    });
    service = TestBed.inject(HttpErrorInterceptorService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
