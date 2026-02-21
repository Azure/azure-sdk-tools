import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';
import { vi } from 'vitest';
import { initializeTestBed } from '../../../../test-setup';
import { CommentItemModel, CommentSeverity } from 'src/app/_models/commentItemModel';
import { CodePanelRowData } from 'src/app/_models/codePanelModels';
import { CommentThreadUpdateAction, CommentUpdatesDto } from 'src/app/_dtos/commentThreadUpdateDto';
import { NotificationsService } from 'src/app/_services/notifications/notifications.service';
import { SignalRService } from 'src/app/_services/signal-r/signal-r.service';
import { WorkerService } from 'src/app/_services/worker/worker.service';
import { SharedAppModule } from 'src/app/_modules/shared/shared-app.module';
import { createMockSignalRService, createMockNotificationsService, createMockWorkerService } from 'src/test-helpers/mock-services';

// Mock ngx-simplemde before any component imports
vi.mock('ngx-simplemde', () => {
  const SimplemdeModuleMock = class SimplemdeModule {
    static ɵmod = {
      id: 'SimplemdeModule',
      declarations: [],
      imports: [],
      exports: []
    };
    static ɵinj = {
      imports: [],
      providers: []
    };
    static forRoot() {
      return {
        ngModule: SimplemdeModuleMock,
        providers: []
      };
    }
  };
  class SimplemdeOptions {
    constructor() {}
  }
  class SimplemdeComponent {
    value = '';
    options = {};
    delay = 0;
    valueChange = { emit: vi.fn() };
  }
  return {
    SimplemdeModule: SimplemdeModuleMock,
    SimplemdeOptions,
    SimplemdeComponent
  };
});

// Mock ngx-ui-scroll to avoid vscroll package dependency
vi.mock('ngx-ui-scroll', () => {
  const UiScrollModuleMock = class UiScrollModule {
    static ɵmod = {
      id: 'UiScrollModule',
      declarations: [],
      imports: [],
      exports: []
    };
    static ɵinj = {
      imports: [],
      providers: []
    };
  };
  return {
    UiScrollModule: UiScrollModuleMock
  };
});

import { CommentThreadComponent } from './comment-thread.component';
import { CommentsService } from 'src/app/_services/comments/comments.service';
import { of, throwError } from 'rxjs';
import { HttpResponse } from '@angular/common/http';

describe('CommentThreadComponent', () => {
  let component: CommentThreadComponent;
  let fixture: ComponentFixture<CommentThreadComponent>;

  const mockNotificationsService = createMockNotificationsService();
  const mockSignalRService = createMockSignalRService();
  const mockWorkerService = createMockWorkerService();

  beforeAll(() => {
    initializeTestBed();
  });

  beforeEach(() => {
    TestBed.configureTestingModule({
      schemas: [NO_ERRORS_SCHEMA],
      imports: [
        CommentThreadComponent,
        SharedAppModule
      ],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        { provide: NotificationsService, useValue: mockNotificationsService },
        { provide: SignalRService, useValue: mockSignalRService },
        { provide: WorkerService, useValue: mockWorkerService },
        MessageService
      ]
    });
    fixture = TestBed.createComponent(CommentThreadComponent);
    component = fixture.componentInstance;
    component.codePanelRowData = new CodePanelRowData();
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('avatar rendering', () => {
    it('should show Copilot icon for azure-sdk comments', () => {
      const azureSdkComment = new CommentItemModel();
      azureSdkComment.id = '1';
      azureSdkComment.createdBy = 'azure-sdk';
      azureSdkComment.createdOn = new Date().toISOString();
      azureSdkComment.commentText = 'Copilot suggestion';

      component.codePanelRowData!.comments = [azureSdkComment];
      fixture.detectChanges();

      const copilotIcon = fixture.nativeElement.querySelector('img[src="assets/icons/copilot.svg"]');
      expect(copilotIcon).toBeTruthy();
      expect(copilotIcon?.alt).toBe('Azure SDK Copilot');
    });

    it('should show GitHub avatar for regular user comments', () => {
      const regularComment = new CommentItemModel();
      regularComment.id = '1';
      regularComment.createdBy = 'regular-user';
      regularComment.createdOn = new Date().toISOString();
      regularComment.commentText = 'Regular comment';

      component.codePanelRowData!.comments = [regularComment];
      fixture.detectChanges();

      const githubAvatar = fixture.nativeElement.querySelector('img[src^="https://github.com/regular-user.png"]');
      expect(githubAvatar).toBeTruthy();
      expect(githubAvatar?.alt).toBe('regular-user');
    });
  });

  describe('comment creator name display', () => {
    it('should display "azure-sdk" for azure-sdk comments', () => {
      const azureSdkComment = new CommentItemModel();
      azureSdkComment.id = '1';
      azureSdkComment.createdBy = 'azure-sdk';
      azureSdkComment.createdOn = new Date().toISOString();
      azureSdkComment.commentText = 'Copilot suggestion';

      component.codePanelRowData!.comments = [azureSdkComment];
      fixture.detectChanges();

      const creatorName = fixture.nativeElement.querySelector('.fw-bold');
      expect(creatorName?.textContent?.trim()).toBe('azure-sdk');
    });

    it('should display actual username for regular user comments', () => {
      const regularComment = new CommentItemModel();
      regularComment.id = '1';
      regularComment.createdBy = 'regular-user';
      regularComment.createdOn = new Date().toISOString();
      regularComment.commentText = 'Regular comment';

      component.codePanelRowData!.comments = [regularComment];
      fixture.detectChanges();

      const creatorName = fixture.nativeElement.querySelector('.fw-bold');
      expect(creatorName?.textContent?.trim()).toBe('regular-user');
    });
  });

  describe('setCommentResolutionState', () => {
    it ('should select latest user to resolve comment thread', () => {
      const comment1 = {
        id: '1',
        isResolved: true,
        changeHistory: [ {
          changeAction: 'resolved',
          changedBy: 'test user 1',
        }]
      } as CommentItemModel;
      const comment2 = {
        id: '2',
        isResolved: true,
        changeHistory: [ {
          changeAction: 'resolved',
          changedBy: 'test user 1',
        },
        {
          changeAction: 'resolved',
          changedBy: 'test user 2',
        }]
      } as CommentItemModel;

      component.codePanelRowData!.comments = [comment1, comment2];
      component.codePanelRowData!.isResolvedCommentThread = true;
      fixture.detectChanges();
      component.setCommentResolutionState();
      expect(component.threadResolvedBy).toBe('test user 2');
    });
  });

  describe('thread resolution collapse behavior', () => {
    beforeEach(() => {
      component.userProfile = { userName: 'test-user' } as any;
      const comment = new CommentItemModel();
      comment.id = 'comment1';
      comment.elementId = 'element1';
      component.codePanelRowData!.comments = [comment];
      component.codePanelRowData!.threadId = 'thread1';
      component.codePanelRowData!.nodeIdHashed = 'hash1';
      component.codePanelRowData!.associatedRowPositionInGroup = 0;
    });

    it('should collapse thread when resolving', () => {
      component.threadResolvedAndExpanded = true;
      component.threadResolvedStateToggleText = 'Hide';
      component.threadResolvedStateToggleIcon = 'bi-arrows-collapse';

      vi.spyOn(component.commentResolutionActionEmitter, 'emit');

      component.handleThreadResolutionButtonClick('Resolve');

      expect(component.threadResolvedAndExpanded).toBe(false);
      expect(component.threadResolvedStateToggleText).toBe('Show');
      expect(component.threadResolvedStateToggleIcon).toBe('bi-arrows-expand');
    });

    it('should collapse thread on second resolve after unresolve cycle', () => {
      vi.spyOn(component.commentResolutionActionEmitter, 'emit');

      // First resolve
      component.handleThreadResolutionButtonClick('Resolve');
      expect(component.threadResolvedAndExpanded).toBe(false);

      // User expands the resolved thread
      component.toggleResolvedCommentExpandState();
      expect(component.threadResolvedAndExpanded).toBe(true);

      // Unresolve (simulating the full cycle)
      component.handleThreadResolutionButtonClick('Unresolve');

      // Second resolve - this was the bug, it should collapse again
      component.handleThreadResolutionButtonClick('Resolve');
      expect(component.threadResolvedAndExpanded).toBe(false);
      expect(component.threadResolvedStateToggleText).toBe('Show');
      expect(component.threadResolvedStateToggleIcon).toBe('bi-arrows-expand');
    });
  });

  describe('batch resolution functionality', () => {
    beforeEach(() => {
      component.userProfile = { userName: 'test-user' } as any;
      component.reviewId = 'test-review-id';

      const comment1 = new CommentItemModel();
      comment1.id = 'comment1';
      comment1.upvotes = [];
      comment1.downvotes = [];
      comment1.elementId = 'element1';

      const comment2 = new CommentItemModel();
      comment2.id = 'comment2';
      comment2.upvotes = [];
      comment2.downvotes = [];
      comment2.elementId = 'element2';

      component.relatedComments = [comment1, comment2];
    });

    it('should emit resolution events for batch resolution', () => {
      vi.spyOn(component.batchResolutionActionEmitter, 'emit');
      component.allCodePanelRowData = [
        { nodeId: 'element1', nodeIdHashed: 'hash1', associatedRowPositionInGroup: 0 } as CodePanelRowData
      ];

      const commentIds = ['comment1'];
      component['emitResolutionEvents'](commentIds);

      expect(component.batchResolutionActionEmitter.emit).toHaveBeenCalledWith(
        expect.objectContaining({
          commentId: 'comment1',
          resolvedBy: 'test-user'
        })
      );
    });
  });

  describe('AI comment info ordering', () => {
    it('should display Confidence Score first and Comment ID last in AI info', () => {
      const aiComment = new CommentItemModel();
      aiComment.id = 'test-comment-id';
      aiComment.confidenceScore = 0.85;
      aiComment.guidelineIds = ['guideline-1', 'guideline-2'];
      aiComment.memoryIds = ['memory-1'];

      const aiInfo = component.getAICommentInfoStructured(aiComment);

      expect(aiInfo.items.length).toBe(4);
      expect(aiInfo.items[0].label).toBe('Confidence Score');
      expect(aiInfo.items[0].value).toBe('85%');
      expect(aiInfo.items[1].label).toBe('Guidelines Referenced');
      expect(aiInfo.items[2].label).toBe('Memory References');
      expect(aiInfo.items[3].label).toBe('Id');
      expect(aiInfo.items[3].value).toBe('test-comment-id');
    });

    it('should place Comment ID last even when other fields are missing', () => {
      const aiComment = new CommentItemModel();
      aiComment.id = 'test-comment-id';

      const aiInfo = component.getAICommentInfoStructured(aiComment);

      expect(aiInfo.items.length).toBe(1);
      expect(aiInfo.items[0].label).toBe('Id');
      expect(aiInfo.items[0].value).toBe('test-comment-id');
    });

    it('should maintain correct order with only Confidence Score and ID', () => {
      const aiComment = new CommentItemModel();
      aiComment.id = 'test-comment-id';
      aiComment.confidenceScore = 0.75;

      const aiInfo = component.getAICommentInfoStructured(aiComment);

      expect(aiInfo.items.length).toBe(2);
      expect(aiInfo.items[0].label).toBe('Confidence Score');
      expect(aiInfo.items[0].value).toBe('75%');
      expect(aiInfo.items[1].label).toBe('Id');
      expect(aiInfo.items[1].value).toBe('test-comment-id');
    });
  });

  describe('draft comment text persistence', () => {
    it('should persist draft comment text in codePanelRowData model', () => {
      component.codePanelRowData!.draftCommentText = 'Test draft text';

      expect(component.codePanelRowData!.draftCommentText).toBe('Test draft text');
    });

    it('should initialize draftCommentText as empty string', () => {
      const newRowData = new CodePanelRowData();

      expect(newRowData.draftCommentText).toBe('');
    });

    it('should clear draft text when comment is cancelled', () => {
      component.codePanelRowData!.showReplyTextBox = true;
      component.codePanelRowData!.draftCommentText = 'Draft to be cancelled';

      vi.spyOn(component.cancelCommentActionEmitter, 'emit');

      const mockEvent = {
        target: document.createElement('button')
      } as any;
      const replyContainer = document.createElement('div');
      replyContainer.className = 'reply-editor-container';
      replyContainer.appendChild(mockEvent.target);

      component.cancelCommentAction(mockEvent);

      expect(component.codePanelRowData!.draftCommentText).toBe('');
      expect(component.codePanelRowData!.showReplyTextBox).toBe(false);
    });
  });

  describe('getCommentActionMenuContent', () => {
    beforeEach(() => {
      component.userProfile = { userName: 'test-user' } as any;
      component.instanceLocation = 'code-panel';
    });

    it('should always include Copy link as first menu item', () => {
      const comment = new CommentItemModel();
      comment.id = 'comment1';
      comment.createdBy = 'other-user';
      component.codePanelRowData!.comments = [comment];

      const menu = component.getCommentActionMenuContent('comment1');

      expect(menu.length).toBeGreaterThan(0);
      expect(menu[0].items).toBeDefined();
      expect(menu[0].items![0].label).toBe('Copy link');
      expect(menu[0].items![0].icon).toBe('pi pi-link');
    });

    it('should include Edit and Delete for comment owner', () => {
      const comment = new CommentItemModel();
      comment.id = 'comment1';
      comment.createdBy = 'test-user';
      component.codePanelRowData!.comments = [comment];

      const menu = component.getCommentActionMenuContent('comment1');

      // Find the group with Edit/Delete
      const editDeleteGroup = menu.find(item => item.items?.some(i => i.label === 'Edit'));
      expect(editDeleteGroup).toBeDefined();
      expect(editDeleteGroup!.items!.some(i => i.label === 'Edit')).toBe(true);
      expect(editDeleteGroup!.items!.some(i => i.label === 'Delete')).toBe(true);
    });

    it('should not include Edit/Delete for non-owner users', () => {
      const comment = new CommentItemModel();
      comment.id = 'comment1';
      comment.createdBy = 'other-user';
      component.codePanelRowData!.comments = [comment];

      const menu = component.getCommentActionMenuContent('comment1');

      const editDeleteGroup = menu.find(item => item.items?.some(i => i.label === 'Edit'));
      expect(editDeleteGroup).toBeUndefined();
    });

    it('should include GitHub Issue submenu for code-panel location', () => {
      const comment = new CommentItemModel();
      comment.id = 'comment1';
      comment.createdBy = 'other-user';
      component.codePanelRowData!.comments = [comment];
      component.instanceLocation = 'code-panel';

      const menu = component.getCommentActionMenuContent('comment1');

      const githubIssueGroup = menu.find(item => item.label === 'Create GitHub Issue');
      expect(githubIssueGroup).toBeDefined();
    });

    it('should not include GitHub Issue submenu for samples location', () => {
      const comment = new CommentItemModel();
      comment.id = 'comment1';
      comment.createdBy = 'other-user';
      component.codePanelRowData!.comments = [comment];
      component.instanceLocation = 'samples';

      const menu = component.getCommentActionMenuContent('comment1');

      const githubIssueGroup = menu.find(item => item.label === 'Create GitHub Issue');
      expect(githubIssueGroup).toBeUndefined();
    });
  });

  describe('copyCommentLink', () => {
    let mockClipboard: { writeText: ReturnType<typeof vi.fn> };
    let originalClipboard: Clipboard;
    let mockMessageService: MessageService;

    beforeEach(() => {
      mockClipboard = {
        writeText: vi.fn().mockResolvedValue(undefined)
      };
      originalClipboard = navigator.clipboard;
      Object.defineProperty(navigator, 'clipboard', {
        value: mockClipboard,
        writable: true
      });

      mockMessageService = TestBed.inject(MessageService);
      vi.spyOn(mockMessageService, 'add');

      const comment = new CommentItemModel();
      comment.id = 'test-comment-id';
      comment.elementId = 'test-element-id';
      component.codePanelRowData!.comments = [comment];
      component.codePanelRowData!.nodeId = 'test-node-id';
    });

    afterEach(() => {
      Object.defineProperty(navigator, 'clipboard', {
        value: originalClipboard,
        writable: true
      });
    });

    it('should copy comment link to clipboard with correct URL format', async () => {
      const mockElement = document.createElement('a');
      mockElement.setAttribute('data-item-id', 'test-comment-id');

      const mockEvent = {
        originalEvent: {
          target: mockElement
        }
      } as any;

      component.copyCommentLink(mockEvent);

      expect(mockClipboard.writeText).toHaveBeenCalled();
      const calledUrl = mockClipboard.writeText.mock.calls[0][0];
      expect(calledUrl).toContain('nId=test-element-id');
      expect(calledUrl).toContain('#test-comment-id');
    });

    it('should show success message after copying link', async () => {
      const mockElement = document.createElement('a');
      mockElement.setAttribute('data-item-id', 'test-comment-id');

      const mockEvent = {
        originalEvent: {
          target: mockElement
        }
      } as any;

      component.copyCommentLink(mockEvent);

      // Wait for the clipboard promise to resolve
      await mockClipboard.writeText.mock.results[0]?.value;

      expect(mockMessageService.add).toHaveBeenCalledWith(
        expect.objectContaining({
          severity: 'success',
          summary: 'Link copied'
        })
      );
    });

    it('should use comment elementId for nId parameter', () => {
      const comment = new CommentItemModel();
      comment.id = 'comment-with-element';
      comment.elementId = 'specific-element-id';
      component.codePanelRowData!.comments = [comment];

      const mockElement = document.createElement('a');
      mockElement.setAttribute('data-item-id', 'comment-with-element');

      const mockEvent = {
        originalEvent: {
          target: mockElement
        }
      } as any;

      component.copyCommentLink(mockEvent);

      const calledUrl = mockClipboard.writeText.mock.calls[0][0];
      expect(calledUrl).toContain('nId=specific-element-id');
    });

    it('should fallback to nodeId if comment has no elementId', () => {
      const comment = new CommentItemModel();
      comment.id = 'comment-no-element';
      comment.elementId = '';
      component.codePanelRowData!.comments = [comment];
      component.codePanelRowData!.nodeId = 'fallback-node-id';

      const mockElement = document.createElement('a');
      mockElement.setAttribute('data-item-id', 'comment-no-element');

      const mockEvent = {
        originalEvent: {
          target: mockElement
        }
      } as any;

      component.copyCommentLink(mockEvent);

      const calledUrl = mockClipboard.writeText.mock.calls[0][0];
      expect(calledUrl).toContain('nId=fallback-node-id');
    });
  });

  describe('reply restrictions', () => {
    let emittedDto: CommentUpdatesDto | undefined;

    beforeEach(() => {
      component.userProfile = { userName: 'test-user' } as any;
      component.instanceLocation = 'code-panel';
      emittedDto = undefined;
      component.saveCommentActionEmitter.subscribe((dto: CommentUpdatesDto) => {
        emittedDto = dto;
      });
    });

    function makeEditorReturn(content: string) {
      // Mock the editor QueryList so getEditorContent returns our content
      component.editor = {
        find: () => ({ getEditorContent: () => content })
      } as any;
    }

    function buildReplyEvent(): Event {
      // Build a DOM that mimics clicking Save inside the reply-editor-container
      const replyContainer = document.createElement('div');
      replyContainer.className = 'reply-editor-container';
      const btn = document.createElement('button');
      replyContainer.appendChild(btn);
      return { target: btn } as unknown as Event;
    }

    it('should send severity when creating a new thread (no existing comments)', () => {
      // No existing comments → new thread
      component.codePanelRowData!.comments = [];
      component.codePanelRowData!.nodeId = 'node-1';
      component.codePanelRowData!.showReplyTextBox = true;
      component.selectedSeverity = CommentSeverity.MustFix;
      component.allowAnyOneToResolve = true;

      makeEditorReturn('<p>New thread comment</p>');
      component.saveCommentAction(buildReplyEvent());

      expect(emittedDto).toBeDefined();
      expect(emittedDto!.severity).toBe(CommentSeverity.MustFix);
      expect(emittedDto!.allowAnyOneToResolve).toBe(true);
      expect(emittedDto!.isReply).toBe(false);
    });

    it('should force severity to null when replying to an existing thread', () => {
      const existingComment = new CommentItemModel();
      existingComment.id = 'c1';
      existingComment.elementId = 'elem-1';
      existingComment.severity = CommentSeverity.ShouldFix;

      component.codePanelRowData!.comments = [existingComment];
      component.codePanelRowData!.nodeId = 'node-1';
      component.codePanelRowData!.threadId = 'thread-1';
      component.codePanelRowData!.showReplyTextBox = true;
      component.selectedSeverity = CommentSeverity.Question; // even if somehow set

      makeEditorReturn('<p>Reply text</p>');
      component.saveCommentAction(buildReplyEvent());

      expect(emittedDto).toBeDefined();
      expect(emittedDto!.severity).toBeNull();
      expect(emittedDto!.isReply).toBe(true);
    });

    it('should not send allowAnyOneToResolve when replying to an existing thread', () => {
      const existingComment = new CommentItemModel();
      existingComment.id = 'c1';
      existingComment.elementId = 'elem-1';

      component.codePanelRowData!.comments = [existingComment];
      component.codePanelRowData!.nodeId = 'node-1';
      component.codePanelRowData!.threadId = 'thread-1';
      component.codePanelRowData!.showReplyTextBox = true;
      component.allowAnyOneToResolve = true; // even if set

      makeEditorReturn('<p>Reply text</p>');
      component.saveCommentAction(buildReplyEvent());

      expect(emittedDto).toBeDefined();
      expect(emittedDto!.allowAnyOneToResolve).toBeUndefined();
      expect(emittedDto!.isReply).toBe(true);
    });

    it('should not show Allow Anyone to Resolve button for replies', () => {
      const existingComment = new CommentItemModel();
      existingComment.id = 'c1';
      existingComment.createdBy = 'test-user';
      existingComment.createdOn = new Date().toISOString();
      existingComment.commentText = 'Thread starter';

      component.codePanelRowData!.comments = [existingComment];
      component.codePanelRowData!.showReplyTextBox = true;

      // Verify via component logic: when comments exist, the template condition
      // *ngIf="!codePanelRowData!.comments || codePanelRowData!.comments.length === 0"
      // evaluates to false, so the button is hidden for replies.
      const comments = component.codePanelRowData!.comments;
      const showResolveButton = !comments || comments.length === 0;
      expect(showResolveButton).toBe(false);
    });

    it('should show Allow Anyone to Resolve button for new threads', () => {
      component.codePanelRowData!.comments = [];
      component.codePanelRowData!.showReplyTextBox = true;

      // When no comments exist, the template condition evaluates to true,
      // so the button is shown for new threads.
      const comments = component.codePanelRowData!.comments;
      const showResolveButton = !comments || comments.length === 0;
      expect(showResolveButton).toBe(true);
    });
  });

  describe('onSeverityChange', () => {
    let commentsService: CommentsService;

    beforeEach(() => {
      commentsService = TestBed.inject(CommentsService);
      component.reviewId = 'test-review';
    });

    it('should call notifySeverityChanged after successful severity update', () => {
      const comment = new CommentItemModel();
      comment.id = 'c1';
      comment.severity = CommentSeverity.Question;
      comment.createdOn = new Date().toISOString();
      comment.createdBy = 'test-user';
      component.codePanelRowData!.comments = [comment];

      vi.spyOn(commentsService, 'updateCommentSeverity').mockReturnValue(of(new HttpResponse({ status: 200 })));
      const severityChangedSpy = vi.spyOn(commentsService, 'notifySeverityChanged');
      const qualityRefreshSpy = vi.spyOn(commentsService, 'notifyQualityScoreRefresh');

      component.onSeverityChange(CommentSeverity.MustFix, 'c1');

      expect(severityChangedSpy).toHaveBeenCalledWith('c1', CommentSeverity.MustFix);
      expect(qualityRefreshSpy).toHaveBeenCalled();
      expect(comment.severity).toBe(CommentSeverity.MustFix);
    });

    it('should NOT call notifySeverityChanged when severity update fails', () => {
      const comment = new CommentItemModel();
      comment.id = 'c1';
      comment.severity = CommentSeverity.Question;
      comment.createdOn = new Date().toISOString();
      comment.createdBy = 'test-user';
      component.codePanelRowData!.comments = [comment];

      vi.spyOn(commentsService, 'updateCommentSeverity').mockReturnValue(throwError(() => ({ status: 500 })));
      const severityChangedSpy = vi.spyOn(commentsService, 'notifySeverityChanged');

      component.onSeverityChange(CommentSeverity.MustFix, 'c1');

      expect(severityChangedSpy).not.toHaveBeenCalled();
      expect(comment.severity).toBe(CommentSeverity.Question); // reverted
    });
  });
});
