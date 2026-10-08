import { TestBed } from '@angular/core/testing';
import { initializeTestBed } from '../../../test-setup';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { APP_BASE_HREF, Location, LocationStrategy, PathLocationStrategy } from '@angular/common';
import { ActivatedRoute } from '@angular/router';

import { APIRevisionsService } from './revisions.service';
import { ConfigService } from '../config/config.service';
import { of } from 'rxjs';
import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';
import { APIRevision } from 'src/app/_models/revision';

describe('RevisionsService', () => {
  let service: APIRevisionsService;

  beforeAll(() => {
    initializeTestBed();
  });

  beforeEach(() => {
    const configServiceMock = {
      apiUrl: 'http://localhost:5000/api',
      webAppUrl: 'http://localhost:5000/',
      loadConfig: () => of({ apiUrl: 'http://localhost:5000/api' }) 
    };

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        Location,
        { provide: LocationStrategy, useClass: PathLocationStrategy },
        { provide: APP_BASE_HREF, useValue: '/' },
        APIRevisionsService,
        { provide: ConfigService, useValue: configServiceMock }
      ]
    });
    service = TestBed.inject(APIRevisionsService);
  });

  afterEach(() => vi.restoreAllMocks());

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

  describe.each(['/', '/spa/browser/'])('navigation with base href %s', baseHref => {
    beforeEach(() => {
      TestBed.resetTestingModule();
      TestBed.configureTestingModule({
        providers: [
          provideHttpClient(),
          provideHttpClientTesting(),
          APIRevisionsService,
          Location,
          { provide: LocationStrategy, useClass: PathLocationStrategy },
          { provide: APP_BASE_HREF, useValue: baseHref },
          { provide: ConfigService, useValue: { apiUrl: '/api/', webAppUrl: 'http://localhost:5000/' } }
        ]
      });
      service = TestBed.inject(APIRevisionsService);
    });

    it.each(['Index', 'Revision'])('opens the selected tree revision from %s', pageName => {
      const route = { data: of({ pageName }) } as ActivatedRoute;
      const revision = { id: 'active', reviewId: 'review', files: [{ parserStyle: 'tree' }] } as APIRevision;
      const open = vi.spyOn(window, 'open').mockReturnValue(null);

      service.openAPIRevisionPage(revision, route);

      expect(open).toHaveBeenCalledExactlyOnceWith(
        `${baseHref}review/review?activeApiRevisionId=active`, pageName === 'Index' ? '_blank' : '_self'
      );
    });

    it.each(['Index', 'Revision'])('opens a tree diff from %s', pageName => {
      const route = { data: of({ pageName }) } as ActivatedRoute;
      const revision = { id: 'active', reviewId: 'review', files: [{ parserStyle: 'tree' }] } as APIRevision;
      const diff = { ...revision, id: 'diff' };
      const open = vi.spyOn(window, 'open').mockReturnValue(null);

      service.openDiffOfAPIRevisions(revision, diff, route);

      expect(open).toHaveBeenCalledExactlyOnceWith(
        `${baseHref}review/review?activeApiRevisionId=active&diffApiRevisionId=diff`,
        pageName === 'Index' ? '_blank' : '_self'
      );
    });

    it('preserves classic legacy revision and diff URLs', () => {
      const route = { data: of({ pageName: 'Revision' }) } as ActivatedRoute;
      const revision = { id: 'active', reviewId: 'review', files: [{ parserStyle: 'flat' }] } as APIRevision;
      const diff = { ...revision, id: 'diff' };
      const open = vi.spyOn(window, 'open').mockReturnValue(null);

      service.openAPIRevisionPage(revision, route);
      service.openDiffOfAPIRevisions(revision, diff, route);

      expect(open.mock.calls).toEqual([
        ['http://localhost:5000/Assemblies/Review/review?revisionId=active', '_self'],
        ['http://localhost:5000/Assemblies/Review/review?revisionId=active&diffOnly=False&doc=False&diffRevisionId=diff', '_self']
      ]);
    });
  });
});
