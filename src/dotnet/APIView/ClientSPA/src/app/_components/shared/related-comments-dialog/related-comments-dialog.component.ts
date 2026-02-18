import { Component, EventEmitter, Input, Output, OnInit, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DialogModule } from 'primeng/dialog';
import { CheckboxModule } from 'primeng/checkbox';
import { SelectModule } from 'primeng/select';
import { MultiSelectModule } from 'primeng/multiselect';
import { TimeagoModule } from 'ngx-timeago';
import { CommentSeverityComponent } from 'src/app/_components/shared/comment-severity/comment-severity.component';
import { MarkdownToHtmlPipe } from 'src/app/_pipes/markdown-to-html.pipe';
import { CommentItemModel, CommentSeverity } from 'src/app/_models/commentItemModel';
import { CodePanelRowData } from 'src/app/_models/codePanelModels';
import { UserProfile } from 'src/app/_models/userProfile';
import { PermissionsService } from 'src/app/_services/permissions/permissions.service';
import { ReviewContextService } from 'src/app/_services/review-context/review-context.service';
import { environment } from 'src/environments/environment';
import { CommentSeverityHelper } from 'src/app/_helpers/comment-severity.helper';
import { AI_COMMENT_FEEDBACK_REASONS } from 'src/app/_models/comment-feedback-reasons';

export type VoteType = 'none' | 'up' | 'down';
export type ConversationDisposition = 'resolve' | 'keepOpen' | 'delete';

export interface CommentResolutionData {
  commentIds: string[];
  batchVote?: VoteType;
  resolutionComment?: string;
  disposition: ConversationDisposition;
  severity?: CommentSeverity;
  feedbackReasons?: string[];
  feedbackAdditionalComments?: string;
}

@Component({
    selector: 'app-related-comments-dialog',
    templateUrl: './related-comments-dialog.component.html',
    styleUrls: ['./related-comments-dialog.component.scss'],
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        DialogModule,
        CheckboxModule,
        SelectModule,
        MultiSelectModule,
        TimeagoModule,
        CommentSeverityComponent,
        MarkdownToHtmlPipe
    ]
})
export class RelatedCommentsDialogComponent implements OnInit, OnChanges {
  @Input() relatedComments: CommentItemModel[] = [];
  @Input() visible: boolean = false;
  @Input() selectedCommentId: string = '';
  @Input() allCodePanelRowData: CodePanelRowData[] = [];
  @Input() userProfile: UserProfile | undefined;
  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() resolveSelectedComments = new EventEmitter<CommentResolutionData>();

  assetsPath: string = environment.assetsPath;
  selectedCommentIds: Set<string> = new Set();
  selectAll: boolean = false;
  batchVote: VoteType | null = null;
  resolutionComment: string = '';
  selectedDisposition: ConversationDisposition = 'keepOpen';
  selectedSeverity: CommentSeverity | null = null;

  showInlineFeedback: boolean = false;
  feedbackExpanded: boolean = true;
  feedbackReasons: string[] = [];
  feedbackAdditionalComments: string = '';

  deletionReason: string = '';

  readonly availableFeedbackReasons = AI_COMMENT_FEEDBACK_REASONS;

  get feedbackReasonOptions() {
    return this.availableFeedbackReasons.map(r => ({ label: r.label, value: r.key }));
  }

  severityOptions = CommentSeverityHelper.severityOptions;

  dispositionOptions = [
    { label: 'Keep Open', value: 'keepOpen' as ConversationDisposition, icon: 'pi pi-comment' },
    { label: 'Resolve', value: 'resolve' as ConversationDisposition, icon: 'pi pi-check-circle' },
    { label: 'Delete', value: 'delete' as ConversationDisposition, icon: 'pi pi-trash' }
  ];

  // Permission check: User can edit severity if they can edit ALL selected comments
  get canEditSeverity(): boolean {
    if (!this.userProfile || this.relatedComments.length === 0) {
      return false;
    }

    // Get the comments that are currently selected (or all if none selected)
    const commentsToCheck = this.selectedCommentIds.size > 0
      ? this.relatedComments.filter(c => this.selectedCommentIds.has(c.id))
      : this.relatedComments;

    // User can edit if they are the owner of ALL comments, or if they are an approver for this language and ALL comments are from azure-sdk bot
    const isApprover = this.permissionsService.isApproverFor(this.userProfile?.permissions, this.reviewContextService.getLanguage());
    return commentsToCheck.every(comment =>
      comment.createdBy === this.userProfile?.userName ||
      (comment.createdBy === 'azure-sdk' && isApprover)
    );
  }

  constructor(private permissionsService: PermissionsService, private reviewContextService: ReviewContextService) { }

  // Performance optimization: Cache for code context
  private codeContextCache = new Map<string, string>();

  ngOnInit() {
    this.resetSelection();
  }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['relatedComments'] || changes['visible']) {
      if (this.visible) {
        this.resetSelection();
        this.codeContextCache.clear();
        this.updateSeverityFromComments();
      }
    }
  }

  private resetSelection() {
    this.selectedCommentIds.clear();
    this.selectAll = false;
    this.batchVote = null;
    this.resolutionComment = '';
    this.deletionReason = '';
    this.selectedDisposition = 'keepOpen'; // Default to safest option
    this.showInlineFeedback = false;
    this.feedbackReasons = [];
    this.feedbackAdditionalComments = '';
    this.updateSeverityFromComments();
  }

  private updateSeverityFromComments() {
    const commentsToCheck = this.selectedCommentIds.size > 0
      ? this.relatedComments.filter(c => this.selectedCommentIds.has(c.id))
      : this.relatedComments;

    const severities = commentsToCheck
      .map(c => CommentSeverityHelper.getSeverityEnumValue(c.severity))
      .filter(s => s !== null) as CommentSeverity[];

    if (severities.length > 0) {
      const allSame = severities.every(s => s === severities[0]);
      if (allSame) {
        this.selectedSeverity = severities[0];
      } else {
        this.selectedSeverity = Math.max(...severities) as CommentSeverity;
      }
    } else {
      this.selectedSeverity = null;
    }
  }

  getPlaceholderText(): string {
    switch (this.selectedDisposition) {
      case 'resolve':
        return 'Add a comment when resolving the selected issues...';
      case 'keepOpen':
        return 'Add a reply to the selected issues...';
      case 'delete':
        return 'Explain why this comment is egregiously wrong and must be deleted...';
      default:
        return 'Add a comment...';
    }
  }

  get isDeletionReasonValid(): boolean {
    return this.selectedDisposition !== 'delete' || this.deletionReason.trim().length > 0;
  }

  onHide() {
    this.visible = false;
    this.visibleChange.emit(false);
    this.resetSelection();
  }

  toggleCommentSelection(commentId: string) {
    if (this.selectedCommentIds.has(commentId)) {
      this.selectedCommentIds.delete(commentId);
    } else {
      this.selectedCommentIds.add(commentId);
    }
    this.updateSelectAllState();
  }

  onSelectAllChange(event: { checked?: boolean }) {
    this.selectAll = event.checked ?? false;
    if (this.selectAll) {
      this.selectedCommentIds.clear();
      this.relatedComments.forEach(comment => {
        this.selectedCommentIds.add(comment.id);
      });
    } else {
      this.selectedCommentIds.clear();
    }
  }

  updateSelectAllState() {
    this.selectAll = this.selectedCommentIds.size === this.relatedComments.length;
  }

  isCommentSelected(commentId: string): boolean {
    return this.selectedCommentIds.has(commentId);
  }

  resolveSelected() {
    if (this.selectedCommentIds.size > 0) {
      const resolutionData: CommentResolutionData = {
        commentIds: Array.from(this.selectedCommentIds),
        batchVote: this.batchVote || undefined,
        resolutionComment: this.selectedDisposition !== 'delete'
          ? this.resolutionComment.trim() || undefined
          : undefined,
        disposition: this.selectedDisposition,
        severity: this.selectedSeverity !== null ? this.selectedSeverity : undefined,
        feedbackReasons: this.feedbackReasons.length > 0 ? this.feedbackReasons : undefined,
        feedbackAdditionalComments: this.selectedDisposition === 'delete'
          ? this.deletionReason.trim() || undefined
          : this.feedbackAdditionalComments.trim() || undefined
      };

      console.log('🔍 Resolution data being emitted:', resolutionData);

      this.resolveSelectedComments.emit(resolutionData);
      this.onHide();
    }
  }

  getSelectedCount(): number {
    return this.selectedCommentIds.size;
  }

  isTriggeringComment(commentId: string): boolean {
    return commentId === this.selectedCommentId;
  }

  toggleBatchVote(voteType: 'up' | 'down') {
    if (this.batchVote === voteType) {
      this.batchVote = null;
      this.showInlineFeedback = false;
    } else {
      this.batchVote = voteType;
      if (voteType === 'down' && this.hasAIGeneratedComments) {
        this.showInlineFeedback = true;
      } else {
        this.showInlineFeedback = false;
      }
    }
  }

  hasBatchUpvote(): boolean {
    return this.batchVote === 'up';
  }

  hasBatchDownvote(): boolean {
    return this.batchVote === 'down';
  }

  get canSubmitFeedback(): boolean {
    return this.feedbackReasons.length > 0;
  }

  get hasAIGeneratedComments(): boolean {
    const commentsToCheck = this.selectedCommentIds.size > 0
      ? this.relatedComments.filter(c => this.selectedCommentIds.has(c.id))
      : this.relatedComments;

    return commentsToCheck.some(c => c.createdBy === 'azure-sdk');
  }

  isFeedbackReasonSelected(reason: string): boolean {
    return this.feedbackReasons.includes(reason);
  }

  toggleFeedbackReason(reason: string): void {
    const index = this.feedbackReasons.indexOf(reason);
    if (index > -1) {
      this.feedbackReasons.splice(index, 1);
    } else {
      this.feedbackReasons.push(reason);
    }
  }

  getCodeContextForComment(comment: CommentItemModel): string {
    // Check cache first for performance
    if (this.codeContextCache.has(comment.id)) {
      return this.codeContextCache.get(comment.id)!;
    }

    if (!comment.elementId || !this.allCodePanelRowData || this.allCodePanelRowData.length === 0) {
      return '';
    }

    const codeRow = this.allCodePanelRowData.find(row => row.nodeId === comment.elementId);
    if (!codeRow || !codeRow.rowOfTokens) {
      return '';
    }

    const result = codeRow.rowOfTokens
      .map(token => token.value)
      .join('')
      .trim();

    // Cache the result for future calls
    this.codeContextCache.set(comment.id, result);
    return result;
  }
}
