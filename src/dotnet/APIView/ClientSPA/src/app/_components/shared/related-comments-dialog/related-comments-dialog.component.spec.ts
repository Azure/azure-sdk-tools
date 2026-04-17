import { vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';

// Mock SimpleMDE to avoid ESM packaging issues in tests
vi.mock('ngx-simplemde', () => ({
  SimplemdeModule: class {
    static ɵmod = { id: 'SimplemdeModule', type: this, declarations: [], imports: [], exports: [] };
    static ɵinj = { imports: [], providers: [] };
    static forRoot() { return { ngModule: this, providers: [] }; }
  },
  SimplemdeOptions: class {},
  SimplemdeComponent: class { value = ''; options = {}; valueChange = { emit: vi.fn() }; }
}));

vi.mock('ngx-ui-scroll', () => ({
  UiScrollModule: class {
    static ɵmod = { id: 'UiScrollModule', type: this, declarations: [], imports: [], exports: [] };
    static ɵinj = { imports: [], providers: [] };
  }
}));

import { initializeTestBed } from '../../../../test-setup';
import { createMockSignalRService, createMockNotificationsService, createMockWorkerService } from 'src/test-helpers/mock-services';
import { RelatedCommentsDialogComponent, CommentResolutionData } from './related-comments-dialog.component';
import { NotificationsService } from 'src/app/_services/notifications/notifications.service';
import { SignalRService } from 'src/app/_services/signal-r/signal-r.service';
import { WorkerService } from 'src/app/_services/worker/worker.service';
import { CommentItemModel } from 'src/app/_models/commentItemModel';
import { CodePanelRowData } from 'src/app/_models/codePanelModels';
import { StructuredToken } from 'src/app/_models/structuredToken';
import { SharedAppModule } from 'src/app/_modules/shared/shared-app.module';
import { ReviewPageModule } from 'src/app/_modules/review-page.module';

describe('RelatedCommentsDialogComponent', () => {
  let component: RelatedCommentsDialogComponent;
  let fixture: ComponentFixture<RelatedCommentsDialogComponent>;

  beforeAll(() => {
    initializeTestBed();
  });

  beforeEach(() => {
    const mockNotificationsService = createMockNotificationsService();
    const mockSignalRService = createMockSignalRService();
    const mockWorkerService = createMockWorkerService();

    TestBed.configureTestingModule({
      imports: [
        RelatedCommentsDialogComponent,
        SharedAppModule,
        ReviewPageModule,
        NoopAnimationsModule
      ],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: NotificationsService, useValue: mockNotificationsService },
        { provide: SignalRService, useValue: mockSignalRService },
        { provide: WorkerService, useValue: mockWorkerService }
      ],
      schemas: [NO_ERRORS_SCHEMA]
    });
    fixture = TestBed.createComponent(RelatedCommentsDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('comment selection', () => {
    beforeEach(() => {
      const comment1 = new CommentItemModel();
      comment1.id = 'comment1';
      const comment2 = new CommentItemModel();
      comment2.id = 'comment2';
      const comment3 = new CommentItemModel();
      comment3.id = 'comment3';

      component.relatedComments = [comment1, comment2, comment3];
    });

    it('should toggle individual comment selection', () => {
      expect(component.isCommentSelected('comment1')).toBeFalsy();

      component.toggleCommentSelection('comment1');
      expect(component.isCommentSelected('comment1')).toBeTruthy();

      component.toggleCommentSelection('comment1');
      expect(component.isCommentSelected('comment1')).toBeFalsy();
    });

    it('should select all comments when selectAll is checked', () => {
      expect(component.getSelectedCount()).toBe(0);

      component.onSelectAllChange({ checked: true });

      expect(component.getSelectedCount()).toBe(3);
      expect(component.isCommentSelected('comment1')).toBeTruthy();
      expect(component.isCommentSelected('comment2')).toBeTruthy();
      expect(component.isCommentSelected('comment3')).toBeTruthy();
    });

    it('should deselect all comments when selectAll is unchecked', () => {
      component.toggleCommentSelection('comment1');
      component.toggleCommentSelection('comment2');
      expect(component.getSelectedCount()).toBe(2);

      component.onSelectAllChange({ checked: false });

      expect(component.getSelectedCount()).toBe(0);
      expect(component.isCommentSelected('comment1')).toBeFalsy();
      expect(component.isCommentSelected('comment2')).toBeFalsy();
    });

    it('should update selectAll state based on individual selections', () => {
      component.toggleCommentSelection('comment1');
      component.toggleCommentSelection('comment2');
      component.updateSelectAllState();
      expect(component.selectAll).toBeFalsy();

      component.toggleCommentSelection('comment3');
      component.updateSelectAllState();
      expect(component.selectAll).toBeTruthy();
    });
  });

  describe('batch voting', () => {
    it('should toggle batch upvote correctly', () => {
      expect(component.batchVote).toBeNull();

      component.toggleBatchVote('up');
      expect(component.batchVote).toBe('up');
      expect(component.hasBatchUpvote()).toBeTruthy();
      expect(component.hasBatchDownvote()).toBeFalsy();
    });

    it('should toggle batch downvote correctly', () => {
      expect(component.batchVote).toBeNull();

      component.toggleBatchVote('down');
      expect(component.batchVote).toBe('down');
      expect(component.hasBatchDownvote()).toBeTruthy();
      expect(component.hasBatchUpvote()).toBeFalsy();
    });

    it('should switch from upvote to downvote', () => {
      component.toggleBatchVote('up');
      expect(component.batchVote).toBe('up');

      component.toggleBatchVote('down');
      expect(component.batchVote).toBe('down');
      expect(component.hasBatchUpvote()).toBeFalsy();
      expect(component.hasBatchDownvote()).toBeTruthy();
    });
  });

  describe('resolution', () => {
    beforeEach(() => {
      const comment1 = new CommentItemModel();
      comment1.id = 'comment1';
      const comment2 = new CommentItemModel();
      comment2.id = 'comment2';

      component.relatedComments = [comment1, comment2];
      component.toggleCommentSelection('comment1');
      component.toggleCommentSelection('comment2');
    });

    it('should emit resolution data with selected comments', () => {
      vi.spyOn(component.resolveSelectedComments, 'emit');
      component.batchVote = 'up';
      component.resolutionComment = 'Test resolution comment';
      component.selectedDisposition = 'keepOpen';

      component.resolveSelected();

      expect(component.resolveSelectedComments.emit).toHaveBeenCalledWith({
        commentIds: ['comment1', 'comment2'],
        batchVote: 'up',
        resolutionComment: 'Test resolution comment',
        disposition: 'keepOpen',
        severity: undefined,
        feedbackReasons: undefined,
        feedbackAdditionalComments: undefined
      } as CommentResolutionData);
    });

    it('should not emit resolution data when no comments selected', () => {
      vi.spyOn(component.resolveSelectedComments, 'emit');
      component.selectedCommentIds.clear();

      component.resolveSelected();

      expect(component.resolveSelectedComments.emit).not.toHaveBeenCalled();
    });

    it('should hide dialog after successful resolution', () => {
      vi.spyOn(component, 'onHide');

      component.resolveSelected();

      expect(component.onHide).toHaveBeenCalled();
    });
  });

  describe('code context', () => {
    beforeEach(() => {
      const codeRow = new CodePanelRowData();
      codeRow.nodeId = 'element1';
      codeRow.rowOfTokens = [
        new StructuredToken('public '),
        new StructuredToken('void '),
        new StructuredToken('methodName()')
      ];

      component.allCodePanelRowData = [codeRow];
    });

    it('should get code context for comment', () => {
      const comment = new CommentItemModel();
      comment.id = 'comment1';
      comment.elementId = 'element1';

      const context = component.getCodeContextForComment(comment);

      expect(context).toBe('public void methodName()');
    });

    it('should return empty string when no matching code row found', () => {
      const comment = new CommentItemModel();
      comment.id = 'comment1';
      comment.elementId = 'nonexistent';

      const context = component.getCodeContextForComment(comment);

      expect(context).toBe('');
    });

    it('should cache code context for performance', () => {
      const comment = new CommentItemModel();
      comment.id = 'comment1';
      comment.elementId = 'element1';

      // First call
      const context1 = component.getCodeContextForComment(comment);
      // Second call should use cache
      const context2 = component.getCodeContextForComment(comment);

      expect(context1).toBe(context2);
      expect(context1).toBe('public void methodName()');
    });
  });

  describe('triggering comment identification', () => {
    it('should identify triggering comment correctly', () => {
      component.selectedCommentId = 'trigger-comment';

      expect(component.isTriggeringComment('trigger-comment')).toBeTruthy();
      expect(component.isTriggeringComment('other-comment')).toBeFalsy();
    });
  });
});
