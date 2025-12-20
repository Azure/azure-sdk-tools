import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DialogModule } from 'primeng/dialog';

export interface AICommentDeleteReason {
  commentId: string;
  reason: string;
}

@Component({
    selector: 'app-ai-comment-delete-dialog',
    templateUrl: './ai-comment-delete-dialog.component.html',
    styleUrls: ['./ai-comment-delete-dialog.component.scss'],
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        DialogModule
    ]
})
export class AICommentDeleteDialogComponent {
  @Input() visible: boolean = false;
  @Input() commentId: string = '';
  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() deleteConfirm = new EventEmitter<AICommentDeleteReason>();
  @Output() cancel = new EventEmitter<void>();

  reason: string = '';

  readonly dialogTitle = "Delete Comment";
  readonly dialogDescription = "Only delete comments that are egregiously wrong or harmful. Please explain why this comment should be removed.";

  get canDelete(): boolean {
    return this.reason.trim().length > 0;
  }

  onDelete(): void {
    if (!this.canDelete) {
      return;
    }

    this.deleteConfirm.emit({
      commentId: this.commentId,
      reason: this.reason
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
    this.reason = '';
  }
}
