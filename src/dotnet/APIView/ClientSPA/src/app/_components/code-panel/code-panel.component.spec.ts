import { ComponentFixture, TestBed } from '@angular/core/testing';
import { initializeTestBed } from '../../../test-setup';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { vi } from 'vitest';
import { NO_ERRORS_SCHEMA } from '@angular/core';

// Mock ngx-ui-scroll module to avoid vscroll dependency
vi.mock('ngx-ui-scroll', () => ({
  Datasource: class MockDatasource {
    adapter = {
      reload: vi.fn(),
      reset: vi.fn(),
      relax: vi.fn()
    };
    settings = {};
  },
  IDatasource: class MockIDatasource {},
  SizeStrategy: {},
  UiScrollModule: {
    forRoot: () => ({})
  }
}));

import { CodePanelComponent } from './code-panel.component';
import { CommentsService } from 'src/app/_services/comments/comments.service';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { MessageService } from 'primeng/api';
import { StructuredToken } from 'src/app/_models/structuredToken';
import { CodePanelRowData, CodePanelRowDatatype } from 'src/app/_models/codePanelModels';
import { CodeDiagnostic } from 'src/app/_models/codeDiagnostic';
import { SignalRService } from 'src/app/_services/signal-r/signal-r.service';
import { Subject, of } from 'rxjs';
import { CommentItemModel } from 'src/app/_models/commentItemModel';
import { CommentThreadUpdateAction, CommentUpdatesDto } from 'src/app/_dtos/commentThreadUpdateDto';
import { CommentSeverity } from 'src/app/_models/commentItemModel';

describe('CodePanelComponent', () => {
  let component: CodePanelComponent;
  let fixture: ComponentFixture<CodePanelComponent>;

  beforeAll(() => initializeTestBed());

  beforeEach(() => {
    // Mock navigator.clipboard in each test context
    Object.defineProperty(navigator, 'clipboard', {
      value: {
        writeText: vi.fn().mockResolvedValue(undefined)
      },
      writable: true,
      configurable: true
    });

    TestBed.configureTestingModule({
      declarations: [CodePanelComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        CommentsService,
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ reviewId: 'test' }),
              queryParamMap: convertToParamMap({ activeApiRevisionId: 'test', diffApiRevisionId: 'test' })
            }
          }
        },
        MessageService,
        // Mock SignalRService to avoid 'hubs/notification' URL resolution error
        {
          provide: SignalRService,
          useValue: {
            commentUpdate$: new Subject(),
            onCommentUpdates: vi.fn().mockReturnValue(new Subject()),
            startConnection: vi.fn(),
            stopConnection: vi.fn()
          }
        }
      ],
      schemas: [NO_ERRORS_SCHEMA] // Ignore unknown elements and attributes
    });
    fixture = TestBed.createComponent(CodePanelComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('diff comment behavior', () => {
    it('should allow adding comments on removed rows in diff mode when row has content', () => {
      const removedRow = new CodePanelRowData();
      removedRow.type = CodePanelRowDatatype.CodeLine;
      removedRow.rowClasses = new Set<string>(['removed']);
      removedRow.rowOfTokens = [{ value: 'removed line' } as any];

      component.isDiffView = true;
      component.userProfile = {} as any;

      expect(component.canAddComment(removedRow)).toBe(true);
    });

    it('should route new comment creation from removed rows to diff revision', () => {
      const commentsService = TestBed.inject(CommentsService);
      const createCommentSpy = vi.spyOn(commentsService, 'createComment').mockReturnValue(of({ threadId: 'new-thread' } as CommentItemModel));
      vi.spyOn(component as any, 'addCommentToCommentThread').mockImplementation(() => {});

      component.reviewId = 'review-1';
      component.activeApiRevisionId = 'active-rev';
      component.diffApiRevisionId = 'diff-rev';
      component.isDiffView = true;
      component.codePanelData = {
        nodeMetaData: {
          'hash-1': {
            codeLines: [{ rowClasses: new Set<string>(['removed']), nodeId: 'line-1' }]
          }
        }
      } as any;

      component.handleSaveCommentActionEmitter({
        nodeId: 'line-1',
        nodeIdHashed: 'hash-1',
        associatedRowPositionInGroup: 0,
        commentText: 'new comment',
        allowAnyOneToResolve: false,
        severity: undefined,
        isReply: false,
        commentThreadUpdateAction: CommentThreadUpdateAction.CommentCreated,
      } as CommentUpdatesDto);

      expect(createCommentSpy).toHaveBeenCalledWith(
        'review-1',
        'diff-rev',
        'line-1',
        'new comment',
        0,
        true,
        undefined,
        undefined
      );
    });

    it('should immediately display new comment on green row when red and green share same elementId', () => {
      const commentsService = TestBed.inject(CommentsService);
      vi.spyOn(commentsService, 'createComment').mockReturnValue(of({
        id: 'comment-1',
        threadId: 'thread-1',
        elementId: 'shared-id'
      } as CommentItemModel));

      const addCommentSpy = vi.spyOn(component as any, 'addCommentToCommentThread').mockImplementation(() => {});
      vi.spyOn(component as any, 'removeItemsFromScroller').mockImplementation(() => Promise.resolve());

      component.reviewId = 'review-1';
      component.activeApiRevisionId = 'active-rev';
      component.diffApiRevisionId = 'diff-rev';
      component.isDiffView = true;
      component.codePanelData = {
        nodeMetaData: {
          redNode: {
            codeLines: [{ rowClasses: new Set<string>(['removed']), nodeId: 'shared-id', nodeIdHashed: 'redNode', rowPositionInGroup: 0 }],
            commentThread: {
              0: [{
                type: CodePanelRowDatatype.CommentThread,
                nodeIdHashed: 'redNode',
                associatedRowPositionInGroup: 0,
                threadId: 'thread-1',
                comments: []
              }]
            }
          },
          greenNode: {
            codeLines: [{ rowClasses: new Set<string>(), nodeId: 'shared-id', nodeIdHashed: 'greenNode', rowPositionInGroup: 0 }],
            commentThread: {}
          }
        }
      } as any;

      component.handleSaveCommentActionEmitter({
        nodeId: 'shared-id',
        nodeIdHashed: 'redNode',
        associatedRowPositionInGroup: 0,
        commentText: 'new comment',
        threadId: 'thread-1',
        allowAnyOneToResolve: false,
        severity: undefined,
        isReply: false,
        commentThreadUpdateAction: CommentThreadUpdateAction.CommentCreated,
      } as CommentUpdatesDto);

      const forwardedUpdates = addCommentSpy.mock.calls[0][0] as CommentUpdatesDto;
      expect(forwardedUpdates.nodeIdHashed).toBe('greenNode');
      expect(forwardedUpdates.associatedRowPositionInGroup).toBe(0);
      expect(forwardedUpdates.nodeId).toBe('shared-id');
    });

    it('should keep non-diff comment creation on active revision', () => {
      const commentsService = TestBed.inject(CommentsService);
      const createCommentSpy = vi.spyOn(commentsService, 'createComment').mockReturnValue(of({ threadId: 'new-thread' } as CommentItemModel));
      vi.spyOn(component as any, 'addCommentToCommentThread').mockImplementation(() => {});

      component.reviewId = 'review-1';
      component.activeApiRevisionId = 'active-rev';
      component.diffApiRevisionId = 'diff-rev';
      component.isDiffView = false;

      component.handleSaveCommentActionEmitter({
        nodeId: 'line-1',
        nodeIdHashed: 'hash-1',
        associatedRowPositionInGroup: 0,
        commentText: 'new comment',
        allowAnyOneToResolve: false,
        severity: undefined,
        isReply: false,
        commentThreadUpdateAction: CommentThreadUpdateAction.CommentCreated,
      } as CommentUpdatesDto);

      expect(createCommentSpy).toHaveBeenCalledWith(
        'review-1',
        'active-rev',
        'line-1',
        'new comment',
        0,
        true,
        undefined,
        undefined
      );
    });
  });

  describe('orphan unresolved thread indicators', () => {
    it('should create indicator for unresolved comment whose elementId is not visible', () => {
      component.activeApiRevisionId = 'active-rev';
      component.codePanelRowData = [
        { type: CodePanelRowDatatype.CodeLine, nodeId: 'visible-id', rowOfTokens: [{ value: 'line' }] } as any
      ];
      component.allComments = [
        {
          id: 'c1',
          threadId: 'thread-1',
          elementId: 'deleted-id',
          createdBy: 'tjprescott',
          apiRevisionId: 'old-rev',
          isResolved: false,
          isDeleted: false,
          severity: CommentSeverity.MustFix
        } as CommentItemModel
      ];

      (component as any).updateOrphanUnresolvedThreadIndicators();

      expect(component.orphanUnresolvedThreadIndicators.length).toBe(1);
      expect(component.orphanUnresolvedThreadIndicators[0].severityLabel).toBe('MUST FIX');
      expect(component.orphanUnresolvedThreadIndicators[0].severityIconClass).toBe('severity-icon-must-fix');
      expect(component.orphanUnresolvedThreadIndicators[0].elementId).toBe('deleted-id');
      expect(component.orphanUnresolvedThreadIndicators[0].comments.length).toBe(1);
      expect(component.orphanUnresolvedThreadIndicators[0].expanded).toBe(false);
    });

    it('should handle string-typed severity from JSON deserialization', () => {
      component.activeApiRevisionId = 'active-rev';
      component.codePanelRowData = [
        { type: CodePanelRowDatatype.CodeLine, nodeId: 'visible-id', rowOfTokens: [{ value: 'line' }] } as any
      ];
      component.allComments = [
        {
          id: 'c1',
          threadId: 'thread-1',
          elementId: 'deleted-id',
          createdBy: 'AlitzelMendez',
          apiRevisionId: 'old-rev',
          isResolved: false,
          isDeleted: false,
          severity: 'ShouldFix' as any
        } as CommentItemModel
      ];

      (component as any).updateOrphanUnresolvedThreadIndicators();

      expect(component.orphanUnresolvedThreadIndicators.length).toBe(1);
      expect(component.orphanUnresolvedThreadIndicators[0].severityLabel).toBe('SHOULD FIX');
      expect(component.orphanUnresolvedThreadIndicators[0].severityIconClass).toBe('severity-icon-should-fix');
    });

    it('should navigate to diff context when showing orphan unresolved thread', () => {
      const navigateSpy = vi.spyOn((component as any).router, 'navigate').mockResolvedValue(true);
      component.activeApiRevisionId = 'active-rev';

      component.showOrphanUnresolvedThread('deleted-id', 'old-rev');

      expect(navigateSpy).toHaveBeenCalled();
      const queryParams = navigateSpy.mock.calls[0][1].queryParams;
      expect(queryParams.diffApiRevisionId).toBe('old-rev');
      expect(queryParams.nId).toBe('deleted-id');
      navigateSpy.mockRestore();
    });

    it('should toggle expanded state of orphan indicator', () => {
      component.activeApiRevisionId = 'active-rev';
      component.codePanelRowData = [
        { type: CodePanelRowDatatype.CodeLine, nodeId: 'visible-id', rowOfTokens: [{ value: 'line' }] } as any
      ];
      component.allComments = [
        {
          id: 'c1',
          threadId: 'thread-1',
          elementId: 'deleted-id',
          createdBy: 'tjprescott',
          apiRevisionId: 'old-rev',
          isResolved: false,
          isDeleted: false,
          commentText: 'This needs fixing',
          severity: CommentSeverity.MustFix
        } as CommentItemModel
      ];

      (component as any).updateOrphanUnresolvedThreadIndicators();
      expect(component.orphanUnresolvedThreadIndicators[0].expanded).toBe(false);
      expect(component.orphanUnresolvedThreadIndicators[0].commentThreadRowData).toBeNull();

      component.toggleOrphanIndicator('thread-1');
      expect(component.orphanUnresolvedThreadIndicators[0].expanded).toBe(true);
      expect(component.orphanUnresolvedThreadIndicators[0].commentThreadRowData).not.toBeNull();
      expect(component.orphanUnresolvedThreadIndicators[0].commentThreadRowData!.comments.length).toBe(1);
      expect(component.orphanUnresolvedThreadIndicators[0].commentThreadRowData!.nodeId).toBe('deleted-id');

      component.toggleOrphanIndicator('thread-1');
      expect(component.orphanUnresolvedThreadIndicators[0].expanded).toBe(false);
    });

    it('should preserve expanded state when indicators are rebuilt', () => {
      component.activeApiRevisionId = 'active-rev';
      component.codePanelRowData = [
        { type: CodePanelRowDatatype.CodeLine, nodeId: 'visible-id', rowOfTokens: [{ value: 'line' }] } as any
      ];
      component.allComments = [
        {
          id: 'c1',
          threadId: 'thread-1',
          elementId: 'deleted-id',
          createdBy: 'tjprescott',
          apiRevisionId: 'old-rev',
          isResolved: false,
          isDeleted: false,
          severity: CommentSeverity.MustFix
        } as CommentItemModel
      ];

      (component as any).updateOrphanUnresolvedThreadIndicators();
      component.toggleOrphanIndicator('thread-1');
      expect(component.orphanUnresolvedThreadIndicators[0].expanded).toBe(true);

      // Re-run the update (simulating data change)
      (component as any).updateOrphanUnresolvedThreadIndicators();
      expect(component.orphanUnresolvedThreadIndicators[0].expanded).toBe(true);
    });
  });

  describe('copyReviewTextToClipBoard', () => {
    it('should copy formatted review text to clipboard', async () => {
      const token1 = new StructuredToken();
      token1.value = 'token1';
      const token2 = new StructuredToken();
      token2.value = 'token2';
      const token3 = new StructuredToken();
      token3.value = 'token3';

      const codePanelRowData1 = new CodePanelRowData();
      codePanelRowData1.rowOfTokens = [token1, token2];
      codePanelRowData1.indent = 2;
      const codePanelRowData2 = new CodePanelRowData();
      codePanelRowData2.rowOfTokens = [token3];

      component.codePanelRowData = [codePanelRowData1, codePanelRowData2];

      await component.copyReviewTextToClipBoard(false);

      expect(navigator.clipboard.writeText).toHaveBeenCalledWith('\ttoken1token2\ntoken3');
    });

    it('should handle empty codePanelRowData', async () => {
      component.codePanelRowData = [];

      await component.copyReviewTextToClipBoard(false);

      expect(navigator.clipboard.writeText).toHaveBeenCalledWith('');
    });

    it('should handle rows without rowOfTokens', async () => {
      const token1 = new StructuredToken();
      token1.value = 'token1';
      const codePanelRowData1 = new CodePanelRowData();
      const codePanelRowData2 = new CodePanelRowData();
      codePanelRowData2.indent = 1;
      codePanelRowData2.rowOfTokens = [token1];
      component.codePanelRowData = [codePanelRowData1, codePanelRowData2];

      await component.copyReviewTextToClipBoard(false);

      expect(navigator.clipboard.writeText).toHaveBeenCalledWith('token1');
    });

    it('should handle rows with indentation correctly', async () => {
      const token1 = new StructuredToken();
      token1.value = 'token1';
      const token2 = new StructuredToken();
      token2.value = 'token2';

      const codePanelRowData1 = new CodePanelRowData();
      codePanelRowData1.rowOfTokens = [token1];
      codePanelRowData1.indent = 2;
      const codePanelRowData2 = new CodePanelRowData();
      codePanelRowData2.rowOfTokens = [token2];
      codePanelRowData2.indent = 1;


      component.codePanelRowData = [codePanelRowData1, codePanelRowData2];

      await component.copyReviewTextToClipBoard(false);

      expect(navigator.clipboard.writeText).toHaveBeenCalledWith('\ttoken1\ntoken2');
    });
  });

  describe('searchCodePanelRowData', () => {
    it('should be defined', () => {
      expect(component.searchCodePanelRowData).toBeDefined();
    });

    it('should handle no matches gracefully', () => {
      const token1 = new StructuredToken();
      token1.value = 'token1';
      const token2 = new StructuredToken();
      token2.value = 'token2';
      const codePanelRowData1 = new CodePanelRowData();
      codePanelRowData1.rowOfTokens = [token1, token2];
      component.codePanelRowData = [codePanelRowData1];
      component.searchCodePanelRowData('nonexistent');
      expect(component.codeLineSearchMatchInfo?.length).toBeUndefined();
    });

    it('should handle an empty search term', () => {
      const token1 = new StructuredToken();
      token1.value = 'token1';
      const token2 = new StructuredToken();
      token2.value = 'token2';
      const codePanelRowData1 = new CodePanelRowData();
      codePanelRowData1.rowOfTokens = [token1, token2];
      component.codePanelRowData = [codePanelRowData1];
      component.searchCodePanelRowData('');
      expect(component.codeLineSearchMatchInfo?.length).toBeUndefined();
    });

    it('should match across different row types', async () => {
      const row1 = new CodePanelRowData(
        CodePanelRowDatatype.Documentation, 1, [
          new StructuredToken("// <summary>", '', "content", new Set(["doc"]), {}, new Set(["comment"])),
          new StructuredToken(" ", '', "nonBreakingSpace")
        ], "Azure.Template.MiniSecretClient.MiniSecretClient(System.Uri, Azure.Core.TokenCredential)", "nodeIdHashed", 0, 0
      );
      const row2 = new CodePanelRowData(
        CodePanelRowDatatype.Documentation, 2, [
          new StructuredToken("// Initializes a new instance of the <see cref=\"T:Azure.Template.MiniSecretClient\" />.", '', "content", new Set(["doc"]), {}, new Set(["comment"])),
          new StructuredToken(" ", '', "nonBreakingSpace")
        ], "Azure.Template.MiniSecretClient.MiniSecretClient(System.Uri, Azure.Core.TokenCredential)", "nodeIdHashed", 1, 0
      );
      const row3 = new CodePanelRowData(
        CodePanelRowDatatype.Documentation, 3, [
          new StructuredToken("// </summary>", '', "content", new Set(["doc"]), {}, new Set(["comment"])),
          new StructuredToken(" ", '', "nonBreakingSpace")
        ], "Azure.Template.MiniSecretClient.MiniSecretClient(System.Uri, Azure.Core.TokenCredential)", "nodeIdHashed", 2, 0
      );
      const row4 = new CodePanelRowData(
        CodePanelRowDatatype.CodeLine, 4, [
          new StructuredToken("public", '', "content", new Set(), {}, new Set(["keyword"])),
          new StructuredToken(" ", '', "nonBreakingSpace"),
          new StructuredToken("MiniSecretClient", '', "content", new Set(), {}, new Set(["tname"])),
          new StructuredToken("(", '', "content", new Set(), {}, new Set(["punc"])),
          new StructuredToken("Uri", '', "content", new Set(), {}, new Set(["tname"])),
          new StructuredToken(" ", '', "nonBreakingSpace"),
          new StructuredToken("endpoint", '', "content", new Set(), {}, new Set(["text"])),
          new StructuredToken(",", '', "content", new Set(), {}, new Set(["punc"])),
          new StructuredToken(" ", '', "nonBreakingSpace"),
          new StructuredToken("TokenCredential", '', "content", new Set(), {}, new Set(["tname"])),
          new StructuredToken(" ", '', "nonBreakingSpace"),
          new StructuredToken("credential", '', "content", new Set(), {}, new Set(["text"])),
          new StructuredToken(";", '', "content", new Set(), {}, new Set(["punc"])),
          new StructuredToken(" ", '', "nonBreakingSpace")
        ], "Azure.Template.MiniSecretClient.MiniSecretClient(System.Uri, Azure.Core.TokenCredential)", "nodeIdHashed", 0, 0
      );
      const row5 = new CodePanelRowData(
        CodePanelRowDatatype.Diagnostics, 5, [], "Azure.Template.MiniSecretClient.MiniSecretClient(System.Uri, Azure.Core.TokenCredential)", "nodeIdHashed", 0, 0,
        new Set(), 0, "noneDiff", '', '', '', new CodeDiagnostic("diagnosticId", "text", "helpLinkUri", "targetId", "level"),
      );

      component.codePanelRowData = [row1, row2, row3, row4, row5];
      await component.searchCodePanelRowData('MiniSecretClient');

      expect(component.searchMatchedRowInfo.size).toBe(2);
      expect(component.codeLineSearchMatchInfo?.length).toBe(2);
      expect(component.codeLineSearchInfo?.currentMatch?.value.nodeIdHashed).toBe('nodeIdHashed');
    });
  });

  describe('highlightSearchMatches', () => {
    it('should be defined', () => {
      expect(component.highlightSearchMatches).toBeDefined();
    });

    it('should clear previous highlights', () => {
      vi.spyOn(component, 'clearSearchMatchHighlights');
      component.highlightSearchMatches();
      expect(component.clearSearchMatchHighlights).toHaveBeenCalled();
    });
  });

  describe('draft comment persistence during virtual scrolling', () => {
    it('should preserve draft comment text when updating items in scroller', async () => {
      const rowData = new CodePanelRowData();
      rowData.nodeIdHashed = 'test-hash';
      rowData.type = CodePanelRowDatatype.CommentThread;
      rowData.associatedRowPositionInGroup = 0;
      rowData.showReplyTextBox = true;
      rowData.draftCommentText = 'User typed this draft';

      component.codePanelRowData = [rowData];

      // Simulate an update (e.g., from comment system)
      const updatedRowData = new CodePanelRowData();
      updatedRowData.nodeIdHashed = 'test-hash';
      updatedRowData.type = CodePanelRowDatatype.CommentThread;
      updatedRowData.associatedRowPositionInGroup = 0;
      updatedRowData.showReplyTextBox = true;
      updatedRowData.draftCommentText = 'User typed this draft'; // Should preserve this

      await component.updateItemInScroller(updatedRowData);

      // Draft text should still be preserved in the array
      expect(component.codePanelRowData[0].draftCommentText).toBe('User typed this draft');
    });

    it('should maintain draft text reference across multiple updates', async () => {
      const rowData = new CodePanelRowData();
      rowData.nodeIdHashed = 'test-hash';
      rowData.type = CodePanelRowDatatype.CommentThread;
      rowData.associatedRowPositionInGroup = 0;
      rowData.draftCommentText = 'Initial draft';

      component.codePanelRowData = [rowData];

      // First update - different property change
      const update1 = new CodePanelRowData();
      update1.nodeIdHashed = 'test-hash';
      update1.type = CodePanelRowDatatype.CommentThread;
      update1.associatedRowPositionInGroup = 0;
      update1.draftCommentText = 'Initial draft';
      update1.showReplyTextBox = true;

      await component.updateItemInScroller(update1);
      expect(component.codePanelRowData[0].draftCommentText).toBe('Initial draft');

      // User types more
      component.codePanelRowData[0].draftCommentText = 'Initial draft with more text';

      // Second update - another property change
      const update2 = new CodePanelRowData();
      update2.nodeIdHashed = 'test-hash';
      update2.type = CodePanelRowDatatype.CommentThread;
      update2.associatedRowPositionInGroup = 0;
      update2.draftCommentText = 'Initial draft with more text';
      update2.showReplyTextBox = true;

      await component.updateItemInScroller(update2);

      // Draft should still be there
      expect(component.codePanelRowData[0].draftCommentText).toBe('Initial draft with more text');
    });
  });

  describe('Quality score refresh on comment actions', () => {
    let commentsService: CommentsService;
    let notifySpy: ReturnType<typeof vi.spyOn>;

    beforeEach(() => {
      commentsService = TestBed.inject(CommentsService);
      notifySpy = vi.spyOn(commentsService, 'notifyQualityScoreRefresh');
      component.reviewId = 'test-review';
      component.activeApiRevisionId = 'test-revision';
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
        nodeId: 'node-1',
        commentText: 'new comment',
        allowAnyOneToResolve: false,
        severity: undefined,
        isReply: false,
        commentThreadUpdateAction: CommentThreadUpdateAction.CommentCreated,
      } as CommentUpdatesDto);

      expect(notifySpy).toHaveBeenCalled();
    });

    it('should NOT call notifyQualityScoreRefresh when adding a reply to an existing thread', () => {
      const mockResponse = { threadId: 'existing-thread' } as CommentItemModel;
      vi.spyOn(commentsService, 'createComment').mockReturnValue(of(mockResponse));
      vi.spyOn(component as any, 'addCommentToCommentThread').mockImplementation(() => {});

      component.handleSaveCommentActionEmitter({
        nodeId: 'node-1',
        commentText: 'reply text',
        threadId: 'existing-thread',
        allowAnyOneToResolve: undefined,
        severity: null,
        isReply: true,
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
      const row = new CodePanelRowData();
      row.type = CodePanelRowDatatype.CommentThread;
      row.nodeIdHashed = 'hash-1';
      row.comments = [
        { id: 'comment-1', severity: CommentSeverity.Suggestion } as CommentItemModel
      ];

      component.codePanelRowData = [row];
      vi.spyOn(component, 'updateItemInScroller').mockImplementation(() => Promise.resolve());

      component.ngOnInit();

      commentsService.notifySeverityChanged('comment-1', CommentSeverity.MustFix);

      expect(row.comments[0].severity).toBe(CommentSeverity.MustFix);
    });

    it('should not fail when severityChanged$ emits for a comment not in code panel', () => {
      const row = new CodePanelRowData();
      row.type = CodePanelRowDatatype.CommentThread;
      row.nodeIdHashed = 'hash-1';
      row.comments = [
        { id: 'comment-1', severity: CommentSeverity.Question } as CommentItemModel
      ];

      component.codePanelRowData = [row];
      vi.spyOn(component, 'updateItemInScroller').mockImplementation(() => Promise.resolve());

      component.ngOnInit();

      // Emit for a comment not in codePanelRowData - should not throw
      commentsService.notifySeverityChanged('comment-999', CommentSeverity.ShouldFix);

      expect(row.comments[0].severity).toBe(CommentSeverity.Question);
    });
  });
});
