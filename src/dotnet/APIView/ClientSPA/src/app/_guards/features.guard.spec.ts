import { TestBed } from '@angular/core/testing';
import { CanActivateFn } from '@angular/router';

import { featuresGuard } from './features.guard';

describe('featuresGuard', () => {
  const executeGuard: CanActivateFn = (...guardParameters) => 
      TestBed.runInInjectionContext(() => featuresGuard(...guardParameters));

  beforeEach(() => {
    TestBed.configureTestingModule({});
  });

  it('should be created', () => {
    expect(executeGuard).toBeTruthy();
  });
});
