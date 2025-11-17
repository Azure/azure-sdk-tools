import { Component, EventEmitter, Input, Output, OnInit, OnChanges, SimpleChanges } from '@angular/core';
import { CommentItemModel, CommentSeverity } from 'src/app/_models/commentItemModel';
import { CodePanelRowData } from 'src/app/_models/codePanelModels';
import { UserProfile } from 'src/app/_models/userProfile';
import { environment } from 'src/environments/environment';
import { CommentSeverityHelper } from 'src/app/_helpers/comment-severity.helper';

export type VoteType = 'none' | 'up' | 'down';
export type ConversationDisposition = 'resolve' | 'keepOpen' | 'delete';

export interface CommentResolutionData {
  commentIds: string[];
  batchVote?: VoteType;
  resolutionComment?: string;
  disposition: ConversationDisposition;
  severity?: CommentSeverity;
}

@Component({
  selector: 'app-related-comments-dialog',
  templateUrl: './related-comments-dialog.component.html',
  styleUrls: ['./related-comments-dialog.component.scss']
})
export class RelatedCommentsDialogComponent implements OnInit, OnChanges {
  @Input() relatedComments: CommentItemModel[] = [];
  @Input() visible: boolean = false;
  @Input() selectedCommentId: string = '';
  @Input() allCodePanelRowData: CodePanelRowData[] = [];
  @Input() userProfile: UserProfile | undefined;
  @Input() preferredApprovers: string[] = [];
  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() resolveSelectedComments = new EventEmitter<CommentResolutionData>();

  assetsPath: string = environment.assetsPath;
  selectedCommentIds: Set<string> = new Set();
  selectAll: boolean = false;
  batchVote: VoteType | null = null;
  resolutionComment: string = '';
  selectedDisposition: ConversationDisposition = 'keepOpen';
  selectedSeverity: CommentSeverity | null = null;

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

    // User can edit if they are the owner of ALL comments, or if they are an architect and ALL comments are from azure-sdk bot
    return commentsToCheck.every(comment => 
      comment.createdBy === this.userProfile?.userName || 
      (comment.createdBy === 'azure-sdk' && this.preferredApprovers.includes(this.userProfile?.userName!))
    );
  }

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
    this.selectedDisposition = 'keepOpen'; // Default to safest option
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
        return 'Add a comment before deleting the selected issues...';
      default:
        return 'Add a comment...';
    }
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
        resolutionComment: this.resolutionComment.trim() || undefined,
        disposition: this.selectedDisposition,
        severity: this.selectedSeverity !== null ? this.selectedSeverity : undefined
      };
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
    } else {
      this.batchVote = voteType;
    }
  }

  hasBatchUpvote(): boolean {
    return this.batchVote === 'up';
  }

  hasBatchDownvote(): boolean {
    return this.batchVote === 'down';
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