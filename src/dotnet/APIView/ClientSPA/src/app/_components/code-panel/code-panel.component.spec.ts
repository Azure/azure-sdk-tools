import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CodePanelComponent } from './code-panel.component';
import { CommentsService } from 'src/app/_services/comments/comments.service';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { SharedAppModule } from 'src/app/_modules/shared/shared-app.module';
import { ReviewPageModule } from 'src/app/_modules/review-page.module';
import { MessageService } from 'primeng/api';
import { StructuredToken } from 'src/app/_models/structuredToken';
import { CodePanelRowData } from 'src/app/_models/codePanelModels';

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

      component.copyReviewTextToClipBoard();

      expect(clipboardSpy).toHaveBeenCalledWith('\ttoken1token2\ntoken3');
    });

    it('should handle empty codePanelRowData', () => {
      component.codePanelRowData = [];

      component.copyReviewTextToClipBoard();

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

      component.copyReviewTextToClipBoard();

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

      component.copyReviewTextToClipBoard();

      expect(clipboardSpy).toHaveBeenCalledWith('\ttoken1\ntoken2');
    });
  });
  
});
