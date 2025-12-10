import { TestBed } from '@angular/core/testing';

import { PullRequestsService } from './pull-requests.service';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ConfigService } from '../config/config.service';
import { of } from 'rxjs';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';

describe('PullRequestsService', () => {
  let service: PullRequestsService;

  beforeEach(() => {
    const configServiceMock = {
      apiUrl: 'http://localhost:5000/',
      loadConfig: () => of({ apiUrl: 'http://localhost:5000/' }) 
    };

    TestBed.configureTestingModule({
    imports: [],
    providers: [
        PullRequestsService,
        { provide: ConfigService, useValue: configServiceMock },
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting()
    ]
});
    service = TestBed.inject(PullRequestsService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
