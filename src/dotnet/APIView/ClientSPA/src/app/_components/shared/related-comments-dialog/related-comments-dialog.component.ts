import { Component, EventEmitter, Input, Output, OnInit, OnChanges, SimpleChanges } from '@angular/core';
import { CommentItemModel } from 'src/app/_models/commentItemModel';
import { CodePanelRowData } from 'src/app/_models/codePanelModels';
import { environment } from 'src/environments/environment';

export type VoteType = 'none' | 'up' | 'down';

export interface CommentResolutionData {
  commentIds: string[];
  batchVote?: VoteType;
  resolutionComment?: string;
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
  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() resolveSelectedComments = new EventEmitter<CommentResolutionData>();

  assetsPath: string = environment.assetsPath;
  selectedCommentIds: Set<string> = new Set();
  selectAll: boolean = false;
  batchVote: VoteType | null = null;
  resolutionComment: string = '';

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
      }
    }
  }

  private resetSelection() {
    this.selectedCommentIds.clear();
    this.selectAll = false;
    this.batchVote = null;
    this.resolutionComment = '';
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
        resolutionComment: this.resolutionComment.trim() || undefined
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