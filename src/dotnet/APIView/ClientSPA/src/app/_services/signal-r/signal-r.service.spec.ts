import { TestBed } from '@angular/core/testing';

import { SignalRService } from './signal-r.service';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { ConfigService } from '../config/config.service';
import { of } from 'rxjs';

describe('SignalRService', () => {
  let service: SignalRService;

  beforeEach(() => {
    const configServiceMock = {
      apiUrl: 'http://localhost:5000/hubs',
      loadConfig: () => of({ apiUrl: 'http://localhost:5000/hubs' }) 
    };

    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [
        SignalRService,
        { provide: ConfigService, useValue: configServiceMock }
      ]
    });

    service = TestBed.inject(SignalRService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
