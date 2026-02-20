import { TestBed } from '@angular/core/testing';
import { initializeTestBed } from '../../../test-setup';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';

import { SamplesRevisionService } from './samples.service';

describe('SamplesService', () => {
  let service: SamplesRevisionService;

  beforeAll(() => {
    initializeTestBed();
  });

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(SamplesRevisionService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
