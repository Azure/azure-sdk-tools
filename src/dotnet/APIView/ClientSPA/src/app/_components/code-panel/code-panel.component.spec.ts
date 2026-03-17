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
