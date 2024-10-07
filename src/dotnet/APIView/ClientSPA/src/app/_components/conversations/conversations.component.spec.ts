import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ConversationsComponent } from './conversations.component';
import { SharedAppModule } from 'src/app/_modules/shared/shared-app.module';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { ReviewPageModule } from 'src/app/_modules/review-page.module';
import { APIRevision } from 'src/app/_models/revision';
import { CommentItemModel } from 'src/app/_models/commentItemModel';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { of } from 'rxjs';

describe('ConversationComponent', () => {
  let component: ConversationsComponent;
  let fixture: ComponentFixture<ConversationsComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      declarations: [ConversationsComponent],
      imports: [
        HttpClientTestingModule,
        ReviewPageModule,
        SharedAppModule
      ],
      providers: [
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ reviewId: 'test' }),
            },
            queryParams: of(convertToParamMap({ activeApiRevisionId: 'test', diffApiRevisionId: 'test' }))
          }
        }
      ]
    });
    fixture = TestBed.createComponent(ConversationsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('createCommentThreads', () => {
    it('should group conversation by elementId and latest API revision of comments', () => {
      const apiRevisions = [
        {
          id: '1',
          createdOn: '2021-10-01T00:00:00Z'
        },
        {
          id: '2',
          createdOn: '2022-10-01T00:00:00Z'
        },
        {
          id: '3',
          createdOn: '2023-10-01T00:00:00Z'
        },
        {
          id: '4',
          createdOn: '2024-10-01T00:00:00Z'
        }
      ] as APIRevision[];

      const comments = [
        {
          id: '1',
          elementId: '1',
          apiRevisionId: '1'
        },
        {
          id: '2',
          elementId: '2',
          apiRevisionId: '1'
        },
        {
          id: '3',
          elementId: '3',
          apiRevisionId: '1'
        },
        {
          id: '4',
          elementId: '1',
          apiRevisionId: '2',
          isResolved: true
        },
        {
          id: '5',
          elementId: '2',
          apiRevisionId: '2'
        },
        {
          id: '6',
          elementId: '3',
          apiRevisionId: '2',
          isResolved: true
        },
        {
          id: '7',
          elementId: '2',
          apiRevisionId: '3'
        },
        {
          id: '8',
          elementId: '2',
          apiRevisionId: '4'
        },
      ] as CommentItemModel[];

      component.apiRevisions = apiRevisions;
      component.comments = comments;
      fixture.detectChanges();
      component.createCommentThreads();

      expect(component.commentThreads.size).toBe(2);

      const keys = Array.from(component.commentThreads.keys());
      expect(keys).toEqual(['2', '4']);
      expect(component.numberOfActiveThreads).toBe(1);
    });
  });
});
