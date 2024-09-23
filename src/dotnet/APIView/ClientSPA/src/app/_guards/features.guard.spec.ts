import { TestBed } from '@angular/core/testing';
import { CanActivateFn } from '@angular/router';

import { FeaturesGuard } from './features.guard';

describe('featuresGuard', () => {
  const executeGuard: CanActivateFn = (...guardParameters) => 
      TestBed.runInInjectionContext(() => FeaturesGuard(...guardParameters));

  beforeEach(() => {
    TestBed.configureTestingModule({});
  });

  it('should be created', () => {
    expect(executeGuard).toBeTruthy();
  });
});
