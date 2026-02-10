import { TestBed } from '@angular/core/testing';
import { initializeTestBed } from '../../../test-setup';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';

import { PullRequestsService } from './pull-requests.service';
import { ConfigService } from '../config/config.service';
import { of } from 'rxjs';

describe('PullRequestsService', () => {
  let service: PullRequestsService;

  beforeAll(() => {
    initializeTestBed();
  });

  beforeEach(() => {
    const configServiceMock = {
      apiUrl: 'http://localhost:5000/',
      loadConfig: () => of({ apiUrl: 'http://localhost:5000/' }) 
    };

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        PullRequestsService,
        { provide: ConfigService, useValue: configServiceMock }
      ]
    });
    service = TestBed.inject(PullRequestsService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
