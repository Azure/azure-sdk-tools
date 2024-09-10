import { TestBed } from '@angular/core/testing';

import { PullRequestsService } from './pull-requests.service';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { ConfigService } from '../config/config.service';
import { of } from 'rxjs';

describe('PullRequestsService', () => {
  let service: PullRequestsService;

  beforeEach(() => {
    const configServiceMock = {
      apiUrl: 'http://localhost:5000/',
      loadConfig: () => of({ apiUrl: 'http://localhost:5000/' }) 
    };

    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [
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
