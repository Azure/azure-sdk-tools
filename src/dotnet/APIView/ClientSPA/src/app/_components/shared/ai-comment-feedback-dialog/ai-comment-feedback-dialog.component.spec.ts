import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormsModule } from '@angular/forms';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { DialogModule } from 'primeng/dialog';
import { CheckboxModule } from 'primeng/checkbox';
import { AICommentFeedbackDialogComponent } from './ai-comment-feedback-dialog.component';

describe('AICommentFeedbackDialogComponent', () => {
  let component: AICommentFeedbackDialogComponent;
  let fixture: ComponentFixture<AICommentFeedbackDialogComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      declarations: [AICommentFeedbackDialogComponent],
      imports: [
        FormsModule,
        DialogModule,
        CheckboxModule,
        NoopAnimationsModule
      ]
    });
    fixture = TestBed.createComponent(AICommentFeedbackDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });



  it('should not allow submission without selected reasons', () => {
    component.selectedReasons = [];
    expect(component.canSubmit).toBeFalsy();
  });

  it('should allow submission with at least one reason selected', () => {
    component.selectedReasons = ['This comment is factually incorrect'];
    expect(component.canSubmit).toBeTruthy();
  });

  it('should allow submission with multiple reasons selected', () => {
    component.selectedReasons = ['This comment is factually incorrect', 'This is an APIView rendering bug'];
    expect(component.canSubmit).toBeTruthy();
  });

  it('should emit feedback on submit with valid reasons', () => {
    spyOn(component.feedbackSubmit, 'emit');
    component.commentId = 'test-123';
    component.selectedReasons = ['This comment is factually incorrect'];
    component.additionalComments = 'Test comment';
    
    component.onSubmit();
    
    expect(component.feedbackSubmit.emit).toHaveBeenCalledWith({
      commentId: 'test-123',
      reasons: ['This comment is factually incorrect'],
      additionalComments: 'Test comment'
    });
  });

  it('should not emit feedback on submit without reasons', () => {
    spyOn(component.feedbackSubmit, 'emit');
    component.commentId = 'test-123';
    component.selectedReasons = [];
    component.additionalComments = 'Test comment';
    
    component.onSubmit();
    
    expect(component.feedbackSubmit.emit).not.toHaveBeenCalled();
  });

  it('should reset form after successful submit', () => {
    component.selectedReasons = ['This comment is factually incorrect'];
    component.additionalComments = 'Test comment';
    
    component.onSubmit();
    
    expect(component.selectedReasons.length).toBe(0);
    expect(component.additionalComments).toBe('');
  });

  it('should reset form on cancel', () => {
    component.selectedReasons = ['This comment is factually incorrect'];
    component.additionalComments = 'Test comment';
    component.visible = true;
    
    spyOn(component.visibleChange, 'emit');
    spyOn(component.cancel, 'emit');
    
    component.onCancel();
    
    expect(component.selectedReasons.length).toBe(0);
    expect(component.additionalComments).toBe('');
    expect(component.visible).toBe(false);
    expect(component.visibleChange.emit).toHaveBeenCalledWith(false);
    expect(component.cancel.emit).toHaveBeenCalled();
  });

  it('should reset form on hide', () => {
    component.selectedReasons = ['This comment is factually incorrect'];
    component.additionalComments = 'Test comment';
    
    spyOn(component.cancel, 'emit');
    
    component.onHide();
    
    expect(component.selectedReasons.length).toBe(0);
    expect(component.additionalComments).toBe('');
    expect(component.cancel.emit).toHaveBeenCalled();
  });
});
