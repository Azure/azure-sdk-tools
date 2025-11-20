import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CommentThreadComponent } from './comment-thread.component';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { SharedAppModule } from 'src/app/_modules/shared/shared-app.module';
import { ReviewPageModule } from 'src/app/_modules/review-page.module';
import { CommentItemModel } from 'src/app/_models/commentItemModel';
import { CodePanelRowData } from 'src/app/_models/codePanelModels';
import { MessageService } from 'primeng/api';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';

describe('CommentThreadComponent', () => {
  let component: CommentThreadComponent;
  let fixture: ComponentFixture<CommentThreadComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      declarations: [CommentThreadComponent],
      imports: [
        HttpClientTestingModule,
        ReviewPageModule,
        SharedAppModule,
        NoopAnimationsModule
      ],
      providers: [
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
      
      const copilotIcon = fixture.nativeElement.querySelector('img[src="spa/assets/icons/copilot.svg"]');
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

    it('should apply upvotes to comments correctly', () => {
      spyOn(component.batchResolutionActionEmitter, 'emit');
      component.allCodePanelRowData = [
        { 
          nodeIdHashed: 'hash1', 
          associatedRowPositionInGroup: 0,
          comments: [{ id: 'comment1' } as CommentItemModel]
        } as CodePanelRowData
      ];
      
      component['applyBatchVotes'](['comment1'], 'up', 'test-user');
      
      expect(component.batchResolutionActionEmitter.emit).toHaveBeenCalledWith(
        jasmine.objectContaining({
          commentThreadUpdateAction: jasmine.any(Number), // CommentUpVoteToggled
          commentId: 'comment1'
        })
      );
    });

    it('should apply downvotes to comments correctly', () => {
      spyOn(component.batchResolutionActionEmitter, 'emit');
      component.allCodePanelRowData = [
        { 
          nodeIdHashed: 'hash1', 
          associatedRowPositionInGroup: 0,
          comments: [{ id: 'comment1' } as CommentItemModel]
        } as CodePanelRowData
      ];
      
      component['applyBatchVotes'](['comment1'], 'down', 'test-user');
      
      expect(component.batchResolutionActionEmitter.emit).toHaveBeenCalledWith(
        jasmine.objectContaining({
          commentThreadUpdateAction: jasmine.any(Number), // CommentDownVoteToggled
          commentId: 'comment1'
        })
      );
    });

    it('should emit resolution events for batch resolution', () => {
      spyOn(component.batchResolutionActionEmitter, 'emit');
      component.allCodePanelRowData = [
        { nodeId: 'element1', nodeIdHashed: 'hash1', associatedRowPositionInGroup: 0 } as CodePanelRowData
      ];
      
      const commentIds = ['comment1'];
      component['emitResolutionEvents'](commentIds);
      
      expect(component.batchResolutionActionEmitter.emit).toHaveBeenCalledWith(
        jasmine.objectContaining({
          commentId: 'comment1',
          resolvedBy: 'test-user'
        })
      );
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
      
      spyOn(component.cancelCommentActionEmitter, 'emit');
      
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
});
