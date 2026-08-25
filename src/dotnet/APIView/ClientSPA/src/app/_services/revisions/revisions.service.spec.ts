import { TestBed } from '@angular/core/testing';
import { initializeTestBed } from '../../../test-setup';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';

import { APIRevisionsService } from './revisions.service';
import { ConfigService } from '../config/config.service';
import { of } from 'rxjs';
import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';
import { APIRevision } from 'src/app/_models/revision';

describe('RevisionsService', () => {
  let service: APIRevisionsService;

  beforeAll(() => {
    initializeTestBed();
  });

  beforeEach(() => {
    const configServiceMock = {
      apiUrl: 'http://localhost:5000/api',
      loadConfig: () => of({ apiUrl: 'http://localhost:5000/api' }) 
    };

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        APIRevisionsService,
        { provide: ConfigService, useValue: configServiceMock }
      ]
    });
    service = TestBed.inject(APIRevisionsService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should share fixed-filter revision queries and cache their results', () => {
    const releasedRevision = { id: 'released', reviewId: 'review-id' } as APIRevision;
    const getAPIRevisionsSpy = vi.spyOn(service, 'getAPIRevisions').mockReturnValue(of({
      result: [releasedRevision]
    }));

    service.getFilteredAPIRevisionOptions('review-id', ['Released']).subscribe();
    service.getFilteredAPIRevisionOptions('review-id', ['Released']).subscribe();

    expect(getAPIRevisionsSpy).toHaveBeenCalledOnce();
    expect(getAPIRevisionsSpy).toHaveBeenCalledWith(
      0, 100, 'review-id', undefined, undefined, ['Released'], 'createdOn', 1, false, false, true
    );
    expect(service.getCachedAPIRevisionOptions('review-id')).toEqual([releasedRevision]);
  });
});
