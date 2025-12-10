import { TestBed } from '@angular/core/testing';

import { HttpErrorInterceptorService } from './http-error-interceptor.service';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';

describe('HttpErrorInterceptorService', () => {
  let service: HttpErrorInterceptorService;

  beforeEach(() => {
    TestBed.configureTestingModule({
    imports: [],
    providers: [
        HttpErrorInterceptorService,
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting()
    ]
});
    service = TestBed.inject(HttpErrorInterceptorService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
