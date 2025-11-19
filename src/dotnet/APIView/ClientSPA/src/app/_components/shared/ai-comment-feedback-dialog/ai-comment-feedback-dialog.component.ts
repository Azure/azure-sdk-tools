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
  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() feedbackSubmit = new EventEmitter<AICommentFeedback>();
  @Output() cancel = new EventEmitter<void>();

  readonly dialogTitle = "Provide additional feedback";
  readonly dialogDescription = "Please tell us why you're downvoting this comment to help us improve:";

  selectedReasons: string[] = [];
  additionalComments: string = '';

  readonly feedbackReasons = [
    'This comment is factually incorrect',
    'This is an APIView rendering bug',
    'This is an accepted APIView rendering choice',
    'This is an accepted SDK design pattern',
    'The guideline cited here is out-of-date'
  ];

  get canSubmit(): boolean {
    return this.selectedReasons.length > 0;
  }

  onSubmit(): void {
    if (!this.canSubmit) {
      return;
    }

    this.feedbackSubmit.emit({
      commentId: this.commentId,
      reasons: this.selectedReasons,
      additionalComments: this.additionalComments
    });

    this.closeDialog();
  }

  onCancel(): void {
    this.cancel.emit();
    this.closeDialog();
  }

  onHide(): void {
    this.cancel.emit();
    this.closeDialog();
  }

  private closeDialog(): void {
    this.resetForm();
    this.visible = false;
    this.visibleChange.emit(false);
  }

  private resetForm(): void {
    this.selectedReasons = [];
    this.additionalComments = '';
  }
}
