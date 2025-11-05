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

  it('should show downvote description when not deleting', () => {
    component.isDeleting = false;
    expect(component.dialogDescription).toBe('Your downvote has been recorded. Please tell us why to help us improve:');
  });

  it('should show deletion description when deleting', () => {
    component.isDeleting = true;
    expect(component.dialogDescription).toBe('The comment has been deleted. Please tell us why to help us improve:');
  });

  it('should not allow submission without selected reasons', () => {
    component.selectedReasons = [];
    expect(component.canSubmit).toBeFalsy();
  });

  it('should allow submission with at least one reason selected', () => {
    component.selectedReasons = ['Information is factually incorrect'];
    expect(component.canSubmit).toBeTruthy();
  });

  it('should allow submission with multiple reasons selected', () => {
    component.selectedReasons = ['Information is factually incorrect', 'APIView tool limitation or quirk'];
    expect(component.canSubmit).toBeTruthy();
  });

  it('should emit feedback on submit with valid reasons', () => {
    spyOn(component.feedbackSubmit, 'emit');
    component.commentId = 'test-123';
    component.selectedReasons = ['Information is factually incorrect'];
    component.additionalComments = 'Test comment';
    
    component.onSubmit();
    
    expect(component.feedbackSubmit.emit).toHaveBeenCalledWith({
      commentId: 'test-123',
      reasons: ['Information is factually incorrect'],
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
    component.selectedReasons = ['Information is factually incorrect'];
    component.additionalComments = 'Test comment';
    
    component.onSubmit();
    
    expect(component.selectedReasons.length).toBe(0);
    expect(component.additionalComments).toBe('');
  });

  it('should reset form on cancel', () => {
    component.selectedReasons = ['Inaccurate or incorrect'];
    component.additionalComments = 'Test comment';
    component.visible = true;
    
    spyOn(component.visibleChange, 'emit');
    spyOn(component.dialogHide, 'emit');
    
    component.onCancel();
    
    expect(component.selectedReasons.length).toBe(0);
    expect(component.additionalComments).toBe('');
    expect(component.visible).toBe(false);
    expect(component.visibleChange.emit).toHaveBeenCalledWith(false);
    expect(component.dialogHide.emit).toHaveBeenCalled();
  });

  it('should reset form on hide', () => {
    component.selectedReasons = ['Inaccurate or incorrect'];
    component.additionalComments = 'Test comment';
    
    spyOn(component.dialogHide, 'emit');
    
    component.onHide();
    
    expect(component.selectedReasons.length).toBe(0);
    expect(component.additionalComments).toBe('');
    expect(component.dialogHide.emit).toHaveBeenCalled();
  });
});
