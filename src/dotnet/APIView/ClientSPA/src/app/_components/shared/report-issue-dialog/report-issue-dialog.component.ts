import { Component, EventEmitter, Input, Output, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DialogModule } from 'primeng/dialog';
import { MessageService } from 'primeng/api';

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

  constructor(private messageService: MessageService) {}

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
    if (!this.canSubmit) return;

    const issueUrl = this.buildGitHubIssueUrl();
    // TODO: replace hardcoded URL with actual created issue URL from backend
    const submittedIssueUrl = 'https://github.com/Azure/azure-sdk-tools/issues/14863';

    const data: ReportIssueData = {
      mode: this.mode,
      description: this.description,
      reviewLink: this.reviewLink,
      issueUrl: issueUrl
    };

    if (this.mode === 'comment') {
      data.commentContext = this.commentContext ?? undefined;
    }

    this.issueSubmit.emit(data);
    this.closeDialog();

    // TODO: replace hardcoded URL with actual created issue URL from backend
    this.messageService.add({
      severity: 'success',
      icon: 'bi bi-check-circle-fill',
      summary: 'Issue submitted',
      detail: 'https://github.com/Azure/azure-sdk-tools/issues/14863',
      life: 10000,
      key: 'bc'
    });
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
    this.description = '';
  }

  private buildGitHubIssueUrl(): string {
    const baseUrl = 'https://github.com/Azure/azure-sdk-tools/issues/new';
    const labels = this.getLabels();
    const title = this.getTitle();
    const body = this.getBody();

    const params = new URLSearchParams();
    params.set('title', title);
    params.set('labels', labels.join(','));
    params.set('body', body);

    return `${baseUrl}?${params.toString()}`;
  }

  private getLabels(): string[] {
    const labels = ['APIView'];
    if (this.mode === 'comment' && this.commentContext?.commentSource === 'copilot') {
      labels.push('AVC');
    }
    return labels;
  }

  private getTitle(): string {
    if (this.mode === 'comment') {
      const source = this.commentContext?.commentSource === 'copilot' ? 'Copilot' : 'APIView';
      return `[APIView] Issue with ${source} comment`;
    }
    return `[APIView] `;
  }

  private getBody(): string {
    const sections: string[] = [];

    if (this.description.trim()) {
      sections.push(`## Description\n${this.description.trim()}`);
    }

    if (this.mode === 'comment' && this.commentContext) {
      const source = this.commentContext.commentSource === 'copilot' ? 'Copilot' : 'APIView';
      const contextLines = [
        `- **Source**: ${source}`,
        `- **Language**: ${this.commentContext.language || 'N/A'}`,
      ];
      if (this.commentContext.codeSnippet) {
        contextLines.push(`- **Code**: \`${this.commentContext.codeSnippet}\``);
      }
      if (this.commentContext.commentText) {
        contextLines.push(`- **Comment**: ${this.commentContext.commentText}`);
      }
      sections.push(`## Comment context\n${contextLines.join('\n')}`);
    }

    if (this.reviewLink) {
      sections.push(`## Review link\n${this.reviewLink}`);
    }

    sections.push('---\n*Submitted via APIView Report Issue dialog*');

    return sections.join('\n\n');
  }
}
