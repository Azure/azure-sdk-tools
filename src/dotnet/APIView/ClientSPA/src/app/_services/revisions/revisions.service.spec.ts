import { TestBed } from '@angular/core/testing';

import { RevisionsService } from './revisions.service';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { ConfigService } from '../config/config.service';
import { of } from 'rxjs';

describe('RevisionsService', () => {
  let service: RevisionsService;

  beforeEach(() => {
    const configServiceMock = {
      apiUrl: 'http://localhost:5000/',
      loadConfig: () => of({ apiUrl: 'http://localhost:3000/' }) 
    };

    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [
        RevisionsService,
        { provide: ConfigService, useValue: configServiceMock }
      ]
    });
    service = TestBed.inject(RevisionsService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
