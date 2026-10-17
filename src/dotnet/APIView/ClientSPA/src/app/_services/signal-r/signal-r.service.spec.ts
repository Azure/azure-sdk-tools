import { TestBed } from '@angular/core/testing';
import { initializeTestBed } from '../../../test-setup';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';

import { SignalRService } from './signal-r.service';
import { ConfigService } from '../config/config.service';
import { of } from 'rxjs';

describe('SignalRService', () => {
  let service: SignalRService;

  beforeAll(() => {
    initializeTestBed();
  });

  beforeEach(() => {
    const configServiceMock = {
      apiUrl: 'http://localhost:5000/api/',
      hubUrl: 'http://localhost:5000/hubs/',
      webAppUrl: 'http://localhost:5000/',
      loadConfig: () => of({ 
        apiUrl: 'http://localhost:5000/api/',
        hubUrl: 'http://localhost:5000/hubs/',
        webAppUrl: 'http://localhost:5000/'
      }) 
    };

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
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
