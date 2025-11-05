import { Component, EventEmitter, Input, Output } from '@angular/core';

export interface AICommentFeedback {
  commentId: string;
  reasons: string[];
  additionalComments: string;
}

@Component({
  selector: 'app-ai-comment-feedback-dialog',
  templateUrl: './ai-comment-feedback-dialog.component.html',
  styleUrls: ['./ai-comment-feedback-dialog.component.scss']
})
export class AICommentFeedbackDialogComponent {
  @Input() visible: boolean = false;
  @Input() commentId: string = '';
  @Input() isDeleting: boolean = false;
  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() feedbackSubmit = new EventEmitter<AICommentFeedback>();
  @Output() dialogHide = new EventEmitter<void>();

 
  dialogTitle: string = "Provide additional feedback"

  get dialogDescription(): string {
    return this.isDeleting 
      ? "The comment has been deleted. Please tell us why to help us improve:"
      : "Your downvote has been recorded. Please tell us why to help us improve:";
  }

  selectedReasons: string[] = [];
  additionalComments: string = '';

  feedbackReasons = [
    'Information is factually incorrect',
    'APIView tool limitation or quirk',
    'Azure SDK design decision/guideline',
    'Other'
  ];


  get canSubmit(): boolean {
    // Require at least one reason to be selected
    return this.selectedReasons.length > 0;
  }

  onSubmit() {
    if (this.canSubmit) {
      this.feedbackSubmit.emit({
        commentId: this.commentId,
        reasons: this.selectedReasons,
        additionalComments: this.additionalComments
      });
      this.resetForm();
    }
  }

  onCancel() {
    this.resetForm();
    this.visible = false;
    this.visibleChange.emit(false);
    this.dialogHide.emit();
  }

  onHide() {
    this.resetForm();
    this.dialogHide.emit();
  }

  private resetForm() {
    this.selectedReasons = [];
    this.additionalComments = '';
  }
}
