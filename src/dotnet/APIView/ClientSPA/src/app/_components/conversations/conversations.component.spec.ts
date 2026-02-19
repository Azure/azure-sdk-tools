import { vi } from 'vitest';
vi.mock('ngx-simplemde', () => ({
  SimplemdeModule: class {
    static ɵmod = { id: 'SimplemdeModule', type: this, declarations: [], imports: [], exports: [] };
    static ɵinj = { imports: [], providers: [] };
    static forRoot() { return { ngModule: this, providers: [] }; }
  },
  SimplemdeOptions: class {},
  SimplemdeComponent: class { value = ''; options = {}; valueChange = { emit: vi.fn() }; }
}));

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { initializeTestBed } from '../../../test-setup';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { createMockSignalRService, createMockNotificationsService } from 'src/test-helpers/mock-services';

import { ConversationsComponent } from './conversations.component';
import { SharedAppModule } from 'src/app/_modules/shared/shared-app.module';
import { ReviewPageModule } from 'src/app/_modules/review-page.module';
import { APIRevision } from 'src/app/_models/revision';
import { CommentItemModel, CommentSource } from 'src/app/_models/commentItemModel';
import { CommentSeverity } from 'src/app/_models/commentItemModel';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { of } from 'rxjs';
import { SignalRService } from 'src/app/_services/signal-r/signal-r.service';
import { NotificationsService } from 'src/app/_services/notifications/notifications.service';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { CommentsService } from 'src/app/_services/comments/comments.service';
import { CommentThreadUpdateAction, CommentUpdatesDto } from 'src/app/_dtos/commentThreadUpdateDto';
import { Review } from 'src/app/_models/review';

describe('ConversationComponent', () => {
  let component: ConversationsComponent;
  let fixture: ComponentFixture<ConversationsComponent>;

  const mockSignalRService = createMockSignalRService();
  const mockNotificationsService = createMockNotificationsService();

  beforeAll(() => {
    initializeTestBed();
  });

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [
        ConversationsComponent,
        ReviewPageModule,
        SharedAppModule
      ],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: SignalRService, useValue: mockSignalRService },
        { provide: NotificationsService, useValue: mockNotificationsService },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ reviewId: 'test' }),
            },
            queryParams: of(convertToParamMap({ activeApiRevisionId: 'test', diffApiRevisionId: 'test' }))
          }
        }
      ],
      schemas: [NO_ERRORS_SCHEMA]
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

    it('should count total unresolved threads correctly with mixed resolved/unresolved', () => {
      const apiRevisions = [
        { id: 'rev-1', createdOn: '2021-10-01T00:00:00Z' },
        { id: 'rev-2', createdOn: '2022-10-01T00:00:00Z' },
        { id: 'rev-3', createdOn: '2023-10-01T00:00:00Z' }
      ] as APIRevision[];

      const comments = [
        // Thread 1: unresolved (2 comments, same elementId)
        { id: 'c1', elementId: 'elem-1', apiRevisionId: 'rev-1', isResolved: false },
        { id: 'c2', elementId: 'elem-1', apiRevisionId: 'rev-2', isResolved: false },
        // Thread 2: resolved (has isResolved=true)
        { id: 'c3', elementId: 'elem-2', apiRevisionId: 'rev-1', isResolved: true },
        // Thread 3: unresolved
        { id: 'c4', elementId: 'elem-3', apiRevisionId: 'rev-2', isResolved: false },
        // Thread 4: resolved
        { id: 'c5', elementId: 'elem-4', apiRevisionId: 'rev-3', isResolved: true }
      ] as CommentItemModel[];

      component.apiRevisions = apiRevisions;
      component.comments = comments;
      component.createCommentThreads();

      // Should count only unresolved threads: elem-1 and elem-3
      expect(component.numberOfActiveThreads).toBe(2);
    });

    it('should resolve all comments in a thread when using threadId', () => {
      const apiRevisions = [
        { id: 'rev-1', createdOn: '2021-10-01T00:00:00Z' },
        { id: 'rev-2', createdOn: '2022-10-01T00:00:00Z' }
      ] as APIRevision[];

      const comments = [
        // Thread with same threadId but different elementIds
        { id: 'c1', elementId: 'elem-1', threadId: 'thread-1', apiRevisionId: 'rev-1', isResolved: false },
        { id: 'c2', elementId: 'elem-1', threadId: 'thread-1', apiRevisionId: 'rev-1', isResolved: false },
        { id: 'c3', elementId: 'elem-1', threadId: 'thread-1', apiRevisionId: 'rev-2', isResolved: false },
        // Separate thread
        { id: 'c4', elementId: 'elem-2', threadId: 'thread-2', apiRevisionId: 'rev-1', isResolved: false }
      ] as CommentItemModel[];

      component.apiRevisions = apiRevisions;
      component.comments = comments;
      component.createCommentThreads();

      // Initially 2 unresolved threads
      expect(component.numberOfActiveThreads).toBe(2);

      // Resolve thread-1
      (component as any).applyCommentResolutionUpdate({
        elementId: 'elem-1',
        threadId: 'thread-1',
        commentThreadUpdateAction: CommentThreadUpdateAction.CommentResolved
      });

      // All 3 comments in thread-1 should be resolved
      const thread1Comments = component.comments.filter(c => c.threadId === 'thread-1');
      expect(thread1Comments.every(c => c.isResolved)).toBe(true);

      // thread-2 should still be unresolved
      const thread2Comments = component.comments.filter(c => c.threadId === 'thread-2');
      expect(thread2Comments.every(c => !c.isResolved)).toBe(true);

      // Now only 1 active thread
      expect(component.numberOfActiveThreads).toBe(1);
    });
  });

  describe('Quality score refresh on comment actions', () => {
    let commentsService: CommentsService;
    let signalRService: any;
    let notifySpy: ReturnType<typeof vi.spyOn>;

    beforeEach(() => {
      commentsService = TestBed.inject(CommentsService);
      signalRService = TestBed.inject(SignalRService);
      notifySpy = vi.spyOn(commentsService, 'notifyQualityScoreRefresh');
      if (!signalRService.pushCommentUpdates) {
        signalRService.pushCommentUpdates = vi.fn();
      }
      component.review = { id: 'test-review' } as Review;
    });

    afterEach(() => {
      notifySpy.mockRestore();
    });

    it('should call notifyQualityScoreRefresh after deleting a comment', () => {
      vi.spyOn(commentsService, 'deleteComment').mockReturnValue(of({}));
      vi.spyOn(component as any, 'deleteCommentFromCommentThread').mockImplementation(() => {});

      component.handleDeleteCommentActionEmitter({
        commentId: 'comment-1',
        commentThreadUpdateAction: CommentThreadUpdateAction.CommentDeleted,
      } as CommentUpdatesDto);

      expect(notifySpy).toHaveBeenCalled();
    });

    it('should call notifyQualityScoreRefresh after resolving a comment', () => {
      vi.spyOn(commentsService, 'resolveComments').mockReturnValue(of({}));
      vi.spyOn(component as any, 'applyCommentResolutionUpdate').mockImplementation(() => {});

      component.handleCommentResolutionActionEmitter({
        elementId: 'element-1',
        threadId: 'thread-1',
        commentThreadUpdateAction: CommentThreadUpdateAction.CommentResolved,
      } as CommentUpdatesDto);

      expect(notifySpy).toHaveBeenCalled();
    });

    it('should call notifyQualityScoreRefresh after unresolving a comment', () => {
      vi.spyOn(commentsService, 'unresolveComments').mockReturnValue(of({}));
      vi.spyOn(component as any, 'applyCommentResolutionUpdate').mockImplementation(() => {});

      component.handleCommentResolutionActionEmitter({
        elementId: 'element-1',
        threadId: 'thread-1',
        commentThreadUpdateAction: CommentThreadUpdateAction.CommentUnResolved,
      } as CommentUpdatesDto);

      expect(notifySpy).toHaveBeenCalled();
    });

    it('should call notifyQualityScoreRefresh after creating a new thread', () => {
      const mockResponse = { threadId: 'new-thread' } as CommentItemModel;
      vi.spyOn(commentsService, 'createComment').mockReturnValue(of(mockResponse));
      vi.spyOn(component as any, 'addCommentToCommentThread').mockImplementation(() => {});

      component.handleSaveCommentActionEmitter({
        revisionId: 'revision-1',
        elementId: 'element-1',
        commentText: 'new comment',
        allowAnyOneToResolve: false,
        severity: undefined,
        commentThreadUpdateAction: CommentThreadUpdateAction.CommentCreated,
      } as CommentUpdatesDto);

      expect(notifySpy).toHaveBeenCalled();
    });

    it('should NOT call notifyQualityScoreRefresh when adding a reply to an existing thread', () => {
      const mockResponse = { threadId: 'existing-thread' } as CommentItemModel;
      vi.spyOn(commentsService, 'createComment').mockReturnValue(of(mockResponse));
      vi.spyOn(component as any, 'addCommentToCommentThread').mockImplementation(() => {});

      component.handleSaveCommentActionEmitter({
        revisionId: 'revision-1',
        elementId: 'element-1',
        commentText: 'reply text',
        threadId: 'existing-thread',
        allowAnyOneToResolve: undefined,
        severity: null,
        commentThreadUpdateAction: CommentThreadUpdateAction.CommentCreated,
      } as CommentUpdatesDto);

      expect(notifySpy).not.toHaveBeenCalled();
    });
  });

  describe('Severity change propagation', () => {
    let commentsService: CommentsService;

    beforeEach(() => {
      commentsService = TestBed.inject(CommentsService);
    });

    it('should update comment severity when severityChanged$ emits', () => {
      component.comments = [
        { id: 'comment-1', severity: CommentSeverity.Suggestion } as CommentItemModel,
        { id: 'comment-2', severity: CommentSeverity.Question } as CommentItemModel
      ];

      commentsService.notifySeverityChanged('comment-1', CommentSeverity.MustFix);

      expect(component.comments[0].severity).toBe(CommentSeverity.MustFix);
      expect(component.comments[1].severity).toBe(CommentSeverity.Question);
    });

    it('should not fail when severityChanged$ emits for a comment not in conversations', () => {
      component.comments = [
        { id: 'comment-1', severity: CommentSeverity.Question } as CommentItemModel
      ];

      commentsService.notifySeverityChanged('comment-999', CommentSeverity.ShouldFix);

      expect(component.comments[0].severity).toBe(CommentSeverity.Question);
    });
  });
});
