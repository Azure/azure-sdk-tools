import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CodePanelComponent } from './code-panel.component';
import { CommentsService } from 'src/app/_services/comments/comments.service';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { SharedAppModule } from 'src/app/_modules/shared/shared-app.module';
import { ReviewPageModule } from 'src/app/_modules/review-page.module';
import { MessageService } from 'primeng/api';
import { StructuredToken } from 'src/app/_models/structuredToken';
import { CodePanelRowData, CodePanelRowDatatype } from 'src/app/_models/codePanelModels';
import { CodeDiagnostic } from 'src/app/_models/codeDiagnostic';

describe('CodePanelComponent', () => {
  let component: CodePanelComponent;
  let fixture: ComponentFixture<CodePanelComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      declarations: [CodePanelComponent],
      providers: [
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
        MessageService
      ],
      imports: [HttpClientTestingModule,
        SharedAppModule,
        ReviewPageModule
      ]
    });
    fixture = TestBed.createComponent(CodePanelComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('copyReviewTextToClipBoard', () => {
    let clipboardSpy: jasmine.Spy;

    beforeEach(() => {
      clipboardSpy = spyOn(navigator.clipboard, 'writeText').and.callFake(() => Promise.resolve());
    });

    it('should copy formatted review text to clipboard', () => {
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

      component.copyReviewTextToClipBoard(false);

      expect(clipboardSpy).toHaveBeenCalledWith('\ttoken1token2\ntoken3');
    });

    it('should handle empty codePanelRowData', () => {
      component.codePanelRowData = [];

      component.copyReviewTextToClipBoard(false);

      expect(clipboardSpy).toHaveBeenCalledWith('');
    });

    it('should handle rows without rowOfTokens', () => {
      const token1 = new StructuredToken();
      token1.value = 'token1';
      const codePanelRowData1 = new CodePanelRowData();
      const codePanelRowData2 = new CodePanelRowData();
      codePanelRowData2.indent = 1;
      codePanelRowData2.rowOfTokens = [token1];
      component.codePanelRowData = [codePanelRowData1, codePanelRowData2];

      component.copyReviewTextToClipBoard(false);

      expect(clipboardSpy).toHaveBeenCalledWith('token1');
    });

    it('should handle rows with indentation correctly', () => {
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

      component.copyReviewTextToClipBoard(false);

      expect(clipboardSpy).toHaveBeenCalledWith('\ttoken1\ntoken2');
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
        new Set(), 0, "noneDiff", '', '', new CodeDiagnostic("diagnosticId", "text", "helpLinkUri", "targetId", "level"),
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
      spyOn(component, 'clearSearchMatchHighlights');
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
});
