import { Component, EventEmitter, Input, Output, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DialogModule } from 'primeng/dialog';
import { MessageService } from 'primeng/api';
import { finalize, take } from 'rxjs/operators';
import { AgentService, ReportIssueRequest } from 'src/app/_services/agent/agent.service';

export interface CommentContext {
  commentId: string;
  commentText: string;
  codeSnippet: string;
  language: string;
  elementId: string;
  commentSource: 'apiview' | 'copilot' | 'unknown';
}

export interface ReportIssueData {
  mode: 'general' | 'comment';
  description: string;
  reviewLink: string | null;
  commentContext?: CommentContext;
  issueUrl: string;
}

@Component({
    selector: 'app-report-issue-dialog',
    templateUrl: './report-issue-dialog.component.html',
    styleUrls: ['./report-issue-dialog.component.scss'],
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        DialogModule
    ]
})
export class ReportIssueDialogComponent implements OnChanges {
  @Input() visible: boolean = false;
  @Input() mode: 'general' | 'comment' = 'general';
  @Input() reviewLink: string | null = null;
  @Input() commentContext: CommentContext | null = null;
  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() issueSubmit = new EventEmitter<ReportIssueData>();
  @Output() cancel = new EventEmitter<void>();

  get dialogTitle(): string {
    return this.mode === 'comment' ? 'Report an issue with this comment' : 'Report an issue';
  }

  description: string = '';
  submitting: boolean = false;
  // Set to true while we are programmatically closing the dialog after a
  // successful submit so the resulting onHide does not also emit a 'cancel'.
  private suppressCancelOnHide: boolean = false;

  constructor(private messageService: MessageService, private agentService: AgentService) {}

  get canSubmit(): boolean {
    return this.description.trim().length > 0;
  }

  get commentSourceLabel(): string {
    if (!this.commentContext) return '';
    return this.commentContext.commentSource === 'copilot' ? 'Copilot' : 'APIView';
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['visible'] && changes['visible'].currentValue === true) {
      this.resetForm();
    }
  }

  onSubmit(): void {
    if (!this.canSubmit || this.submitting) return;

    const request: ReportIssueRequest = {
      description: this.description.trim(),
      reviewLink: this.reviewLink ?? undefined,
    };

    if (this.mode === 'comment' && this.commentContext) {
      request.commentId = this.commentContext.commentId || undefined;
      request.language = this.commentContext.language || undefined;
    }

    this.submitting = true;
    this.agentService.reportIssue(request)
      .pipe(
        take(1),
        finalize(() => this.submitting = false)
      )
      .subscribe({
        next: (response) => {
          const data: ReportIssueData = {
            mode: this.mode,
            description: this.description,
            reviewLink: this.reviewLink,
            issueUrl: response.issueUrl
          };
          if (this.mode === 'comment') {
            data.commentContext = this.commentContext ?? undefined;
          }
          this.issueSubmit.emit(data);
          this.suppressCancelOnHide = true;
          this.closeDialog();

          this.messageService.add({
            severity: 'success',
            icon: 'bi bi-check-circle-fill',
            summary: 'Issue submitted',
            detail: response.issueUrl,
            life: 10000,
            key: 'bc'
          });
        },
        error: (err) => {
          console.error('Report issue failed', err);
          this.messageService.add({
            severity: 'error',
            icon: 'bi bi-exclamation-triangle-fill',
            summary: 'Failed to file issue',
            detail: 'Please try again. If the problem persists, contact the APIView team.',
            life: 10000,
            key: 'bc'
          });
        }
      });
  }

  onCancel(): void {
    this.cancel.emit();
    this.closeDialog();
  }

  onHide(): void {
    if (this.suppressCancelOnHide) {
      this.suppressCancelOnHide = false;
      return;
    }
    this.cancel.emit();
    this.closeDialog();
  }

  private closeDialog(): void {
    this.resetForm();
    this.visible = false;
    this.visibleChange.emit(false);
  }

  private resetForm(): void {
    this.description = '';
    this.submitting = false;
  }
}
