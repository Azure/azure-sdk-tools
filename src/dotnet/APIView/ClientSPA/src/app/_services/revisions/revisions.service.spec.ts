import { TestBed } from '@angular/core/testing';

import { APIRevisionsService } from './revisions.service';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ConfigService } from '../config/config.service';
import { of } from 'rxjs';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';

describe('RevisionsService', () => {
  let service: APIRevisionsService;

  beforeEach(() => {
    const configServiceMock = {
      apiUrl: 'http://localhost:5000/api',
      loadConfig: () => of({ apiUrl: 'http://localhost:5000/api' }) 
    };

    TestBed.configureTestingModule({
    imports: [],
    providers: [
        APIRevisionsService,
        { provide: ConfigService, useValue: configServiceMock },
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting()
    ]
});
    service = TestBed.inject(APIRevisionsService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
