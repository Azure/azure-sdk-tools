import { TestBed } from '@angular/core/testing';

import { SignalRService } from './signal-r.service';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ConfigService } from '../config/config.service';
import { of } from 'rxjs';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';

describe('SignalRService', () => {
  let service: SignalRService;

  beforeEach(() => {
    const configServiceMock = {
      apiUrl: 'http://localhost:5000/hubs',
      loadConfig: () => of({ apiUrl: 'http://localhost:5000/hubs' }) 
    };

    TestBed.configureTestingModule({
    imports: [],
    providers: [
        SignalRService,
        { provide: ConfigService, useValue: configServiceMock },
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting()
    ]
});

    service = TestBed.inject(SignalRService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
