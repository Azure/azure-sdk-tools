import { TestBed } from '@angular/core/testing';
import { initializeTestBed } from '../../../test-setup';
import { ConfigService } from './config.service';
import { of } from 'rxjs';

describe('ConfigService', () => {
  let service: ConfigService;

  beforeAll(() => {
    initializeTestBed();
  });

  beforeEach(() => {
    const configServiceMock = {
      loadConfig: () => of({ apiUrl: 'api/', webAppUrl: 'http://localhost:5000/' })  // return Observable of config
    };

    TestBed.configureTestingModule({
      providers: [
        { provide: ConfigService, useValue: configServiceMock }
      ]
    });
    service = TestBed.inject(ConfigService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});