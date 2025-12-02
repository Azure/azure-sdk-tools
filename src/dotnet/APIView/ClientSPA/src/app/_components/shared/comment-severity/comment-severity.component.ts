import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommentSeverity } from 'src/app/_models/commentItemModel';
import { CommentSeverityHelper } from 'src/app/_helpers/comment-severity.helper';

@Component({
  selector: 'app-comment-severity',
  templateUrl: './comment-severity.component.html',
  styleUrls: ['./comment-severity.component.scss']
})
export class CommentSeverityComponent {
  @Input() severity: CommentSeverity | string | null | undefined;
  @Input() canEdit: boolean = false;
  @Input() commentId: string = '';
  @Input() editable: boolean = true; // For bulk dialog, we want dropdown always open
  @Output() severityChange = new EventEmitter<CommentSeverity>();

  isEditingMode: boolean = false;

  severityOptions = CommentSeverityHelper.severityOptions;

  get currentSeverityValue(): CommentSeverity | null {
    return CommentSeverityHelper.getSeverityEnumValue(this.severity);
  }

  get severityLabel(): string {
    return CommentSeverityHelper.getSeverityLabel(this.severity);
  }

  get severityBadgeClass(): string {
    return CommentSeverityHelper.getSeverityBadgeClass(this.severity);
  }

  get hasSeverity(): boolean {
    return this.severity !== null && this.severity !== undefined;
  }

  startEditing(): void {
    if (this.canEdit) {
      this.isEditingMode = true;
    }
  }

  onSeverityChange(newSeverity: CommentSeverity): void {
    this.severityChange.emit(newSeverity);
  }

  onDropdownHide(): void {
    this.isEditingMode = false;
  }
}
