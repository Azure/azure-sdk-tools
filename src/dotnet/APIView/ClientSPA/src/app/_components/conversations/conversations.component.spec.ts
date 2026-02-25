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
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { of } from 'rxjs';
import { SignalRService } from 'src/app/_services/signal-r/signal-r.service';
import { NotificationsService } from 'src/app/_services/notifications/notifications.service';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { CommentThreadUpdateAction } from 'src/app/_dtos/commentThreadUpdateDto';

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

    it('should unresolve only the specific thread and not affect other threads on the same element', () => {
      const apiRevisions = [
        { id: 'rev-1', createdOn: '2021-10-01T00:00:00Z' },
        { id: 'rev-2', createdOn: '2022-10-01T00:00:00Z' }
      ] as APIRevision[];

      const comments = [
        // Thread 1 on elem-1: resolved
        { id: 'c1', elementId: 'elem-1', threadId: 'thread-1', apiRevisionId: 'rev-1', isResolved: true },
        { id: 'c2', elementId: 'elem-1', threadId: 'thread-1', apiRevisionId: 'rev-1', isResolved: true },
        // Thread 2 on same elem-1: also resolved (different thread on same element)
        { id: 'c3', elementId: 'elem-1', threadId: 'thread-2', apiRevisionId: 'rev-1', isResolved: true },
        // Thread 3 on elem-2: unresolved
        { id: 'c4', elementId: 'elem-2', threadId: 'thread-3', apiRevisionId: 'rev-1', isResolved: false }
      ] as CommentItemModel[];

      component.apiRevisions = apiRevisions;
      component.comments = comments;
      component.createCommentThreads();

      // Initially 1 unresolved thread (thread-3)
      expect(component.numberOfActiveThreads).toBe(1);

      // Unresolve only thread-1 (should NOT affect thread-2 even though they share elem-1)
      (component as any).applyCommentResolutionUpdate({
        elementId: 'elem-1',
        threadId: 'thread-1',
        commentThreadUpdateAction: CommentThreadUpdateAction.CommentUnResolved
      });

      // Comments in thread-1 should now be unresolved
      const thread1Comments = component.comments.filter(c => c.threadId === 'thread-1');
      expect(thread1Comments.every(c => !c.isResolved)).toBe(true);

      // Thread-2 should still be resolved (not affected by unresolving thread-1)
      const thread2Comments = component.comments.filter(c => c.threadId === 'thread-2');
      expect(thread2Comments.every(c => c.isResolved)).toBe(true);

      // thread-3 should still be unresolved
      const thread3Comments = component.comments.filter(c => c.threadId === 'thread-3');
      expect(thread3Comments.every(c => !c.isResolved)).toBe(true);

      // Now 2 active threads (thread-1 and thread-3)
      expect(component.numberOfActiveThreads).toBe(2);
    });

    it('should correctly resolve/unresolve legacy comments without threadId using elementId as fallback', () => {
      const apiRevisions = [
        { id: 'rev-1', createdOn: '2021-10-01T00:00:00Z' },
        { id: 'rev-2', createdOn: '2022-10-01T00:00:00Z' }
      ] as APIRevision[];

      const comments = [
        // Legacy thread on elem-1: no threadId, resolved
        { id: 'c1', elementId: 'elem-1', threadId: '', apiRevisionId: 'rev-1', isResolved: true },
        { id: 'c2', elementId: 'elem-1', threadId: '', apiRevisionId: 'rev-1', isResolved: true },
        // Normal thread on elem-2: has threadId, unresolved
        { id: 'c3', elementId: 'elem-2', threadId: 'thread-2', apiRevisionId: 'rev-1', isResolved: false }
      ] as CommentItemModel[];

      component.apiRevisions = apiRevisions;
      component.comments = comments;
      component.createCommentThreads();

      // Initially 1 unresolved thread (thread-2)
      expect(component.numberOfActiveThreads).toBe(1);

      // Unresolve legacy thread — the update payload uses elementId as the threadId (fallback behavior)
      (component as any).applyCommentResolutionUpdate({
        elementId: 'elem-1',
        threadId: 'elem-1', // threadId equals elementId in legacy/fallback case
        commentThreadUpdateAction: CommentThreadUpdateAction.CommentUnResolved
      });

      // Legacy comments on elem-1 should now be unresolved
      const legacyComments = component.comments.filter(c => c.elementId === 'elem-1');
      expect(legacyComments.every(c => !c.isResolved)).toBe(true);

      // thread-2 should still be unresolved (not affected)
      const thread2Comments = component.comments.filter(c => c.threadId === 'thread-2');
      expect(thread2Comments.every(c => !c.isResolved)).toBe(true);

      // Now 2 active threads (legacy elem-1 thread and thread-2)
      expect(component.numberOfActiveThreads).toBe(2);

      // Resolve the legacy thread back
      (component as any).applyCommentResolutionUpdate({
        elementId: 'elem-1',
        threadId: 'elem-1',
        commentThreadUpdateAction: CommentThreadUpdateAction.CommentResolved
      });

      // Legacy comments should be resolved again
      const legacyCommentsAfterResolve = component.comments.filter(c => c.elementId === 'elem-1');
      expect(legacyCommentsAfterResolve.every(c => c.isResolved)).toBe(true);

      // Back to 1 active thread
      expect(component.numberOfActiveThreads).toBe(1);
    });
  });
});
