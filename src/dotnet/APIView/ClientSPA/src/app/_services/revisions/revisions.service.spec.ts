import { TestBed } from '@angular/core/testing';

import { APIRevisionsService } from './revisions.service';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { ConfigService } from '../config/config.service';
import { of } from 'rxjs';

describe('RevisionsService', () => {
  let service: APIRevisionsService;

  beforeEach(() => {
    const configServiceMock = {
      apiUrl: 'http://localhost:5000/api',
      loadConfig: () => of({ apiUrl: 'http://localhost:5000/api' }) 
    };

    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [
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
