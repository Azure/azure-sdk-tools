import 'reflect-metadata';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { RevisionOptionsComponent } from './revision-options.component';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { ReviewPageModule } from 'src/app/_modules/review-page.module';
import { SharedAppModule } from 'src/app/_modules/shared/shared-app.module';

describe('ApiRevisionOptionsComponent', () => {
  let component: RevisionOptionsComponent;
  let fixture: ComponentFixture<RevisionOptionsComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      declarations: [RevisionOptionsComponent],
      imports: [
        SharedAppModule,
        ReviewPageModule
      ],
      providers: [
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ reviewId: 'test' }),
              queryParamMap: convertToParamMap({ activeApiRevisionId: 'test', diffApiRevisionId: 'test' })
            }
          }
        }
      ]
    });
    fixture = TestBed.createComponent(RevisionOptionsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('Tag APIRevision appropriately based on date and/or status', () => {
    const apiRevisions = [
      {
        id: '1',
        isApproved: false,
        version: "12.15.1",
        apiRevisionType: 'manual',
      },
      {
        id: '2',
        isApproved: true,
        version: "12.20.0-beta.2",
        changeHistory: [
          {
            changeAction: 'approved',
            changedOn: '2024-07-01T00:00:00Z',
          }
        ],
        isReleased: true,
        releasedOn: '2024-07-02T00:00:00Z',
        apiRevisionType: 'automatic',
        lastUpdatedOn: '2024-07-01T00:00:00Z',
      },
      {
        id: '3',
        isApproved: true,
        version: "12.20.0",
        changeHistory: [
          {
            changeAction: 'approved',
            changedOn: '2024-07-04T00:00:00Z',
          }
        ],
        isReleased: true,
        releasedOn: '2024-07-05T00:00:00Z',
        apiRevisionType: 'automatic',
        lastUpdatedOn: '2024-07-04T00:00:00Z',
      },
      {
        id: '4',
        isApproved: true,
        version: "12.21.1",
        changeHistory: [
          {
            changeAction: 'approved',
            changedOn: '2024-07-05T00:00:00Z',
          }
        ],
        isReleased: false,
        apiRevisionType: 'automatic',
        lastUpdatedOn: '2024-07-05T00:00:00Z',
      },
      {
        id: '5',
        isApproved: false,
        version: "13.0.0",
        isReleased: false,
        apiRevisionType: 'automatic',
        lastUpdatedOn: '2024-07-04T00:00:00Z',
      },
      {
        id: '6',
        isApproved: true,
        version: "11.0.0",
        changeHistory: [
          {
            changeAction: 'approved',
            changedOn: '2021-07-04T00:00:00Z',
          }
        ],
        isReleased: true,
        releasedOn: '2021-07-05T00:00:00Z',
        apiRevisionType: 'automatic',
        lastUpdatedOn: '2021-07-04T00:00:00Z',
      },
    ];
 
    it('should correctly tag the latest GA APIRevision', () => {
      var result = component.tagLatestGARevision(apiRevisions);
      expect(result.id).toEqual('3');
      expect(result.isLatestGA).toBeTruthy();
    });

    it('should correctly tag the latest approved APIRevision', () => {
      var result = component.tagLatestApprovedRevision(apiRevisions);
      expect(result.id).toEqual('4');
      expect(result.isLatestApproved).toBeTruthy();
    });

    it('should correctly tag the latest automatic APIRevision', () => {
      var result = component.tagCurrentMainRevision(apiRevisions);
      expect(result.id).toEqual('4');
      expect(result.isLatestMain).toBeTruthy();
    });

    it('should correctly tag the latest released APIRevision', () => {
      var result = component.tagLatestReleasedRevision(apiRevisions);
      expect(result.id).toEqual('3');
      expect(result.isLatestReleased).toBeTruthy();
    });
  })
});
