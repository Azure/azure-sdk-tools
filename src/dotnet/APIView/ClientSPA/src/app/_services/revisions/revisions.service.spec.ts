import { TestBed } from '@angular/core/testing';
import { initializeTestBed } from '../../../test-setup';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';

import { APIRevisionsService } from './revisions.service';
import { ConfigService } from '../config/config.service';
import { of } from 'rxjs';

describe('RevisionsService', () => {
  let service: APIRevisionsService;

  beforeAll(() => {
    initializeTestBed();
  });

  beforeEach(() => {
    const configServiceMock = {
      apiUrl: 'http://localhost:5000/api',
      loadConfig: () => of({ apiUrl: 'http://localhost:5000/api' }) 
    };

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        APIRevisionsService,
        { provide: ConfigService, useValue: configServiceMock }
      ]
    });
    service = TestBed.inject(APIRevisionsService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
