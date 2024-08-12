import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CommentThreadComponent } from './comment-thread.component';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { SharedAppModule } from 'src/app/_modules/shared/shared-app.module';
import { CodePanelRowData } from 'src/app/_models/codePanelModels';
import { ReviewPageModule } from 'src/app/_modules/review-page/review-page.module';
import { CommentItemModel } from 'src/app/_models/commentItemModel';

describe('CommentThreadComponent', () => {
  let component: CommentThreadComponent;
  let fixture: ComponentFixture<CommentThreadComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      declarations: [CommentThreadComponent],
      imports: [
        HttpClientTestingModule,
        ReviewPageModule,
        SharedAppModule
      ],
    });
    fixture = TestBed.createComponent(CommentThreadComponent);
    component = fixture.componentInstance;
    component.codePanelRowData = new CodePanelRowData();
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('setCommentResolutionState', () => {
    it ('should select latest user to resolve comment thread', () => {
      const comment1 = {
        id: '1',
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
      
      const codePanelRowData = new CodePanelRowData();
      codePanelRowData.comments = [comment1, comment2];
      codePanelRowData.isResolvedCommentThread = true;
      component.codePanelRowData = codePanelRowData
      fixture.detectChanges();
      expect(component.threadResolvedBy).toBe('test user 2');
    });
  });
});
