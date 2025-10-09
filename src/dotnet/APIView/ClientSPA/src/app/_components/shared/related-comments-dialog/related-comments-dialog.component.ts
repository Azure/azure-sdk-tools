import { Component, EventEmitter, Input, Output, OnInit, OnChanges, SimpleChanges } from '@angular/core';
import { CommentItemModel } from 'src/app/_models/commentItemModel';
import { CodePanelRowData } from 'src/app/_models/codePanelModels';
import { environment } from 'src/environments/environment';

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
  @Output() resolveSelectedComments = new EventEmitter<string[]>();

  assetsPath: string = environment.assetsPath;
  selectedCommentIds: Set<string> = new Set();
  selectAll: boolean = false;

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
      this.resolveSelectedComments.emit(Array.from(this.selectedCommentIds));
      this.onHide();
    }
  }

  getSelectedCount(): number {
    return this.selectedCommentIds.size;
  }

  isTriggeringComment(commentId: string): boolean {
    return commentId === this.selectedCommentId;
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