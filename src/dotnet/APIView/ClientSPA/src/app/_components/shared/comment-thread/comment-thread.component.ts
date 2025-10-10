import { ChangeDetectorRef, Component, EventEmitter, Input, Output, QueryList, SimpleChanges, ViewChildren } from '@angular/core';
import { MenuItem, MenuItemCommandEvent, MessageService } from 'primeng/api';
import { Menu } from 'primeng/menu';
import { environment } from 'src/environments/environment';
import { EditorComponent } from '../editor/editor.component';
import { CodePanelRowData } from 'src/app/_models/codePanelModels';
import { UserProfile } from 'src/app/_models/userProfile';
import { CommentThreadUpdateAction, CommentUpdatesDto } from 'src/app/_dtos/commentThreadUpdateDto';
import { CodeLineRowNavigationDirection } from 'src/app/_helpers/common-helpers';
import { CommentSeverity } from 'src/app/_models/commentItemModel';
import { CommentsService } from 'src/app/_services/comments/comments.service';
import { CommentItemModel } from 'src/app/_models/commentItemModel';
import { CommentRelationHelper } from 'src/app/_helpers/comment-relation.helper';

@Component({
  selector: 'app-comment-thread',
  templateUrl: './comment-thread.component.html',
  styleUrls: ['./comment-thread.component.scss'],
  host: {
    'class': 'user-comment-content'
  },
})
export class CommentThreadComponent {
  @Input() codePanelRowData: CodePanelRowData | undefined = undefined;
  @Input() associatedCodeLine: CodePanelRowData | undefined;
  @Input() actualLineNumber: number = 0;
  @Input() instanceLocation: "code-panel" | "conversations" | "samples" = "code-panel";
  @Input() preferredApprovers : string[] = [];
  @Input() reviewId: string = '';
  @Input() allComments: CommentItemModel[] = [];
  @Input() allCodePanelRowData: CodePanelRowData[] = [];

  @Input() userProfile : UserProfile | undefined;
  @Output() cancelCommentActionEmitter : EventEmitter<any> = new EventEmitter<any>();
  @Output() saveCommentActionEmitter : EventEmitter<any> = new EventEmitter<any>();
  @Output() deleteCommentActionEmitter : EventEmitter<any> = new EventEmitter<any>();
  @Output() commentResolutionActionEmitter : EventEmitter<any> = new EventEmitter<any>();
  @Output() commentUpvoteActionEmitter : EventEmitter<any> = new EventEmitter<any>();
  @Output() commentDownvoteActionEmitter : EventEmitter<any> = new EventEmitter<any>();
  @Output() commentThreadNavaigationEmitter : EventEmitter<any> = new EventEmitter<any>();

  @ViewChildren(Menu) menus!: QueryList<Menu>;
  @ViewChildren(EditorComponent) editor!: QueryList<EditorComponent>;
  
  assetsPath : string = environment.assetsPath;
  menuItemAllUsers: MenuItem[] = [];
  menuItemsLoggedInUsers: MenuItem[] = [];
  menuItemsLoggedInArchitects: MenuItem[] = [];
  allowAnyOneToResolve : boolean = false; // Default to false since default severity is "Should fix"

  threadResolvedBy : string | undefined = '';
  threadResolvedStateToggleText : string = 'Show';
  threadResolvedStateToggleIcon : string = 'bi-arrows-expand';
  threadResolvedAndExpanded : boolean = false;
  spacingBasedOnResolvedState: string = 'my-2';
  resolveThreadButtonText : string = 'Resolve';
  isThreadCollapsed: boolean = false;

  floatItemStart : string = ""
  floatItemEnd : string = ""

  selectedSeverity: CommentSeverity | null = CommentSeverity.ShouldFix; // Default to "Should fix"
  isEditingSeverity: string | null = null; // Track which comment severity is being edited
  severityOptions = [
    { label: 'Question', value: CommentSeverity.Question },
    { label: 'Suggestion', value: CommentSeverity.Suggestion },
    { label: 'Should fix', value: CommentSeverity.ShouldFix },
    { label: 'Must fix', value: CommentSeverity.MustFix }
  ];

  showRelatedCommentsDialog: boolean = false;
  relatedComments: CommentItemModel[] = [];
  selectedCommentId: string = ''; 

  private visibleRelatedCommentsCache = new Map<string, CommentItemModel[]>();

  get canEditSeverity(): boolean {
    if (!this.codePanelRowData?.comments || this.codePanelRowData.comments.length === 0) {
      return false;
    }
    
    const firstComment = this.codePanelRowData.comments[0];
    return firstComment.createdBy === this.userProfile?.userName || 
           (firstComment.createdBy === 'azure-sdk' && this.preferredApprovers.includes(this.userProfile?.userName!));
  }

  CodeLineRowNavigationDirection = CodeLineRowNavigationDirection;

  constructor(private changeDetectorRef: ChangeDetectorRef, private messageService: MessageService, private commentsService: CommentsService) { }

  ngOnInit(): void {
    this.menuItemsLoggedInUsers.push({
      label: '',
      items: [
        { label: 'Edit', icon: 'bi bi-pencil-square', command: (event) => this.showEditEditor(event) },
        { label: 'Delete', icon: 'bi bi-trash', command: (event) => this.deleteComment(event) },
        { separator: true }
      ]
    });

    this.menuItemsLoggedInArchitects.push({
      label: '',
      items: [
        { label: 'Delete', icon: 'bi bi-trash', command: (event) => this.deleteComment(event) },
        { separator: true }
      ]
    });

    this.menuItemAllUsers.push({
      label: 'Create Github Issue',
      items: [{
          title: "csharp",
          label: ".NET",
          command: (event) => this.createGitHubIssue(event),
        },
        {
          title: "java",
          label: "Java",
          command: (event) => this.createGitHubIssue(event),
        },
        {
          title: "python",
          label: "Python",
          command: (event) => this.createGitHubIssue(event),
        },
        {
          title: "c",
          label: "C",
          command: (event) => this.createGitHubIssue(event),
        },
        {
          title: "javascript",
          label: "JavaScript",
          command: (event) => this.createGitHubIssue(event),
        },
        {
          title: "go",
          label: "Go",
          command: (event) => this.createGitHubIssue(event),
        },
        {
          title: "cplusplus",
          label: "C++",
          command: (event) => this.createGitHubIssue(event),
        },
      ]
    });
  }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['codePanelRowData']) {
      this.setCommentResolutionState();
    }
    
    if (changes['allComments'] || changes['allCodePanelRowData']) {
      if (this.allComments && this.allComments.length > 0) {
        CommentRelationHelper.calculateRelatedComments(this.allComments);
      }
      this.visibleRelatedCommentsCache.clear();
    }
  }

  setCommentResolutionState() {
    if (this.codePanelRowData?.isResolvedCommentThread) {
      this.threadResolvedBy = this.codePanelRowData?.commentThreadIsResolvedBy;
      if (!this.threadResolvedBy) {
        const lastestResolvedComment = Array.from(this.codePanelRowData?.comments || []).reverse().find(comment => comment.isResolved && comment.changeHistory && comment.changeHistory.some(ch => ch.changeAction === 'resolved'));
        if (lastestResolvedComment) {
          this.threadResolvedBy = lastestResolvedComment.changeHistory.reverse().find(ch => ch.changeAction === 'resolved')?.changedBy;
        }
      }    
      this.spacingBasedOnResolvedState = (this.instanceLocation === "code-panel") ? 'mb-2' : "";
      this.resolveThreadButtonText = 'Unresolve';
    }
    else {
      this.threadResolvedBy = '';
      this.spacingBasedOnResolvedState = (this.instanceLocation === "code-panel") ? 'my-2' : "";
      this.resolveThreadButtonText = 'Resolve';
    }
    this.setCssPropertyBasedonInstance();
  }

  setCssPropertyBasedonInstance() {
    this.floatItemStart = (this.instanceLocation === 'code-panel') ? "float-start" : "";
    this.floatItemEnd = (this.instanceLocation === 'code-panel') ? "float-end" : "";
  }

  getCommentActionMenuContent(commentId: string) {
    const comment = this.codePanelRowData!.comments?.find(comment => comment.id === commentId);
    const menu : MenuItem[] = [];
    if (comment && this.userProfile?.userName === comment.createdBy) {
      menu.push(...this.menuItemsLoggedInUsers);
    } else if (comment && comment.createdBy == "azure-sdk" && this.preferredApprovers.includes(this.userProfile?.userName!)) {
      menu.push(...this.menuItemsLoggedInArchitects);
    }
    if (this.instanceLocation !== "samples") {
      menu.push(...this.menuItemAllUsers);
    }
    return menu;
  }

  toggleActionMenu(event: any, commentId: string) {
    const menu: Menu | undefined = this.menus.find(menu => menu.el.nativeElement.getAttribute('data-menu-id') === commentId);
    if (menu) {
      menu.toggle(event);
    }
  }

  toggleAllowAnyOneToResolve() {
    this.allowAnyOneToResolve = !this.allowAnyOneToResolve;
  }

  toggleThreadCollapse() {
    this.isThreadCollapsed = !this.isThreadCollapsed;
    
    if (this.isThreadCollapsed) {
      this.stopEditingSeverity();
    }
  }

  createGitHubIssue(event: MenuItemCommandEvent) {
    let repo = "";
    switch (event.item?.title) {
      case "csharp":
        repo = "azure-sdk-for-net";
        break;
      case "java":
        repo = "azure-sdk-for-java";
        break;
      case "python":
        repo = "azure-sdk-for-python";
        break;
      case "c":
        repo = "azure-sdk-for-c";
        break;
      case "javascript":
        repo = "azure-sdk-for-js";
        break;
      case "go":
        repo = "azure-sdk-for-go";
        break;
      case "cplusplus":
        repo = "azure-sdk-for-cpp";
        break;
    }

    const target = (event.originalEvent?.target as Element).closest("a") as Element;
    const commentId = target.getAttribute("data-item-id");
    const commentData = this.codePanelRowData?.comments?.find(comment => comment.id === commentId)?.commentText.replace(/<[^>]*>/g, '').trim();

    let codeLineContent = this.associatedCodeLine 
        ? this.associatedCodeLine.rowOfTokens
            .map(token => token.value)
            .join('')
        : '';

    if (!codeLineContent) {
      codeLineContent = this.codePanelRowData?.comments[0].elementId!;
    }

    const nodeId: string = this.codePanelRowData?.nodeId ?? 'defaultNodeId';
    const apiViewUrl = `${window.location.href.split("#")[0]}&nId=${encodeURIComponent(nodeId)}`;
    const issueBody = encodeURIComponent(`\`\`\`${event.item?.title}\n${codeLineContent}\n\`\`\`\n#\n${commentData}\n#\n[Created from ApiView comment](${apiViewUrl})`);

    window.open(`https://github.com/Azure/${repo}/issues/new?body=${issueBody}`, '_blank');
  }

  showReplyEditor(event: Event) {
    this.codePanelRowData!.showReplyTextBox = true;
  }

  deleteComment(event: MenuItemCommandEvent) {
    const target = (event.originalEvent?.target as Element).closest("a") as Element;
    const commentId = target.getAttribute("data-item-id");
    const title = target.getAttribute("data-element-id");
    this.deleteCommentActionEmitter.emit(
      {
        commentThreadUpdateAction: CommentThreadUpdateAction.CommentDeleted,
        nodeIdHashed: this.codePanelRowData!.nodeIdHashed,
        commentId: commentId,
        associatedRowPositionInGroup: this.codePanelRowData!.associatedRowPositionInGroup,
        title: title // Used for Sample Instance of CommentThread
      } as CommentUpdatesDto
    );
  }

  showEditEditor = (event: MenuItemCommandEvent) => {
    const target = (event.originalEvent?.target as Element).closest("a") as Element;
    const commentId = target.getAttribute("data-item-id");
    this.codePanelRowData!.comments!.find(comment => comment.id === commentId)!.isInEditMode = true;
  }

  cancelCommentAction(event: Event) {
    const target = event.target as Element;
    const replyEditorContainer = target.closest(".reply-editor-container") as Element;
    const title = target.closest(".user-comment-thread")?.getAttribute("title");
    if (replyEditorContainer) {
      this.codePanelRowData!.showReplyTextBox = false;
      this.selectedSeverity = null;
      this.cancelCommentActionEmitter.emit(
        {
          nodeIdHashed: this.codePanelRowData!.nodeIdHashed,
          associatedRowPositionInGroup: this.codePanelRowData!.associatedRowPositionInGroup,
          title: title // Used for Sample Instance of CommentThread
        }
      );
    } else {
      const panel = target.closest("p-panel") as Element;
      const commentId = panel.getAttribute("data-comment-id");
      this.codePanelRowData!.comments!.find(comment => comment.id === commentId)!.isInEditMode = false;
    }
  }

  saveCommentAction(event: Event) {
    const target = event.target as Element;
    const replyEditorContainer = target.closest(".reply-editor-container") as Element;
    let revisionIdForConversationGroup: string | null | undefined = null;
    let elementId: string | null | undefined = null;

    if (this.instanceLocation === "conversations") {
      revisionIdForConversationGroup = target.closest(".conversation-group-revision-id")?.getAttribute("data-conversation-group-revision-id");
      elementId = (target.closest(".conversation-group-threads")?.getElementsByClassName("conversation-group-element-id")[0] as HTMLElement).innerText;
    } else if (this.instanceLocation === "samples") {
      elementId = target.closest(".user-comment-thread")?.getAttribute("title");
    }

    const HTML_STRIP_REGEX = /<\/?[^>]+(>|$)/g
    const emptyCommentContentWarningMessage = { severity: 'info', icon: 'bi bi-info-circle', summary: "Comment Info", detail: "Comment content is empty. No action taken.", key: 'bc', life: 3000 };


    if (replyEditorContainer) {
      const content = this.getEditorContent("replyEditor");
      const contentText = content.replace(HTML_STRIP_REGEX, '');
      if (contentText.length === 0) {
        this.messageService.add(emptyCommentContentWarningMessage);
      } else {
        this.saveCommentActionEmitter.emit(
          { 
            commentThreadUpdateAction: CommentThreadUpdateAction.CommentCreated,
            nodeId: this.codePanelRowData!.nodeId,
            nodeIdHashed: this.codePanelRowData!.nodeIdHashed,
            commentText: content,
            allowAnyOneToResolve: this.allowAnyOneToResolve,
            associatedRowPositionInGroup: this.codePanelRowData!.associatedRowPositionInGroup,
            elementId: elementId,
            revisionId: revisionIdForConversationGroup,
            severity: this.selectedSeverity
          } as CommentUpdatesDto
        );
        this.selectedSeverity = null;
      }
      this.codePanelRowData!.showReplyTextBox = false;
    } else {
      const panel = target.closest("p-panel") as Element;
      const commentId = panel.getAttribute("data-comment-id");
      const content = this.getEditorContent(commentId!);
      const contentText = content.replace(HTML_STRIP_REGEX, '');
      if (contentText.length === 0) {
        this.messageService.add(emptyCommentContentWarningMessage);
      } else {
        this.saveCommentActionEmitter.emit(
          { 
            commentThreadUpdateAction: CommentThreadUpdateAction.CommentTextUpdate,
            nodeId: this.codePanelRowData!.nodeId,
            nodeIdHashed: this.codePanelRowData!.nodeIdHashed,
            commentId: commentId,
            commentText: content,
            associatedRowPositionInGroup: this.codePanelRowData!.associatedRowPositionInGroup,
            elementId: elementId,
            revisionId: revisionIdForConversationGroup
          } as CommentUpdatesDto
        );
      }
      this.codePanelRowData!.comments!.find(comment => comment.id === commentId)!.isInEditMode = false;
    }
  }

  getEditorContent(editorId: string) : string{
    if (this.editor) {
      const replyEditor = this.editor.find(e => e.editorId === editorId);
      return replyEditor?.getEditorContent() || "";
    }
    return "";
  }

  toggleUpVoteAction(event: Event) {
    const target = (event.target as Element).closest("button") as Element;
    const commentId = target.getAttribute("data-btn-id");
    this.commentUpvoteActionEmitter.emit(
      { 
        commentThreadUpdateAction: CommentThreadUpdateAction.CommentUpVoteToggled,
        nodeIdHashed: this.codePanelRowData!.nodeIdHashed,
        commentId: commentId,
        associatedRowPositionInGroup: this.codePanelRowData!.associatedRowPositionInGroup
      } as CommentUpdatesDto
    );
  }

  toggleDownVoteAction(event: Event) {
    const target = (event.target as Element).closest("button") as Element;
    const commentId = target.getAttribute("data-btn-id");
    this.commentDownvoteActionEmitter.emit(
      { 
        commentThreadUpdateAction: CommentThreadUpdateAction.CommentDownVoteToggled,
        nodeIdHashed: this.codePanelRowData!.nodeIdHashed,
        commentId: commentId,
        associatedRowPositionInGroup: this.codePanelRowData!.associatedRowPositionInGroup
      } as CommentUpdatesDto
    );
  }

  toggleResolvedCommentExpandState() {
    this.threadResolvedAndExpanded = !this.threadResolvedAndExpanded;
    if (this.threadResolvedAndExpanded) {
      this.threadResolvedStateToggleText = 'Hide';
      this.threadResolvedStateToggleIcon = 'bi-arrows-collapse';
    } else {
      this.threadResolvedStateToggleText = 'Show';
      this.threadResolvedStateToggleIcon = 'bi-arrows-expand';
    }
    this.setCssPropertyBasedonInstance();
  }

  handleThreadResolutionButtonClick(action: string) {
    this.commentResolutionActionEmitter.emit(
      { 
        commentThreadUpdateAction: (action == "Resolve") ? CommentThreadUpdateAction.CommentResolved  : CommentThreadUpdateAction.CommentUnResolved,
        elementId: this.codePanelRowData!.comments[0].elementId,
        nodeIdHashed: this.codePanelRowData!.nodeIdHashed,
        associatedRowPositionInGroup: this.codePanelRowData!.associatedRowPositionInGroup,
        resolvedBy: this.userProfile?.userName
      } as CommentUpdatesDto
    );
  }

  handleCommentThreadNavaigation(event: Event, direction: CodeLineRowNavigationDirection) {
    const target = (event.target as Element).closest(".user-comment-thread")?.parentNode as Element;
    const targetIndex = target.getAttribute("data-sid");
    this.commentThreadNavaigationEmitter.emit({
      commentThreadNavaigationPointer: targetIndex,
      direction: direction
    });
  }
  
  handleContentEmitter(event: string) {
    this.changeDetectorRef.detectChanges();
  }

  isFirstCommentInThread(comment: any): boolean {
    if (!this.codePanelRowData?.comments) return false;
    return this.codePanelRowData.comments[0].id === comment.id;
  }
  
  private normalizeSeverity(severity: CommentSeverity | string | null | undefined): string | null {
    if (severity === null || severity === undefined) return null;
    return typeof severity === 'string' ? severity.toLowerCase() : CommentSeverity[severity]?.toLowerCase() || null;
  }

  getSeverityLabel(severity: CommentSeverity | string | null | undefined): string {
    const normalized = this.normalizeSeverity(severity);
    switch (normalized) {
      case 'question': return 'Question';
      case 'suggestion': return 'Suggestion';
      case 'shouldfix': return 'Should fix';
      case 'mustfix': return 'Must fix';
      default: return '';
    }
  }

  getSeverityBadgeClass(severity: CommentSeverity | string | null | undefined): string {
    const normalized = this.normalizeSeverity(severity);
    switch (normalized) {
      case 'question': return 'severity-question';
      case 'suggestion': return 'severity-suggestion';
      case 'shouldfix': return 'severity-should-fix';
      case 'mustfix': return 'severity-must-fix';
      default: return '';
    }
  }

  getSeverityEnumValue(severity: CommentSeverity | string | null | undefined): CommentSeverity | null {
    if (severity === null || severity === undefined) return null;
    if (typeof severity === 'number') return severity; 
    
    const normalized = this.normalizeSeverity(severity);
    switch (normalized) {
      case 'question': return CommentSeverity.Question;
      case 'suggestion': return CommentSeverity.Suggestion;
      case 'shouldfix': return CommentSeverity.ShouldFix;
      case 'mustfix': return CommentSeverity.MustFix;
      default: return null;
    }
  }

  onSeverityDropdownHide(): void {
    this.stopEditingSeverity();
  }

  onSeverityChange(newSeverity: CommentSeverity, commentId: string): void {
    // Update the comment's severity value locally first
    const comment = this.codePanelRowData?.comments?.find(c => c.id === commentId);
    if (comment && this.reviewId && this.reviewId.trim() !== '') {
      const originalSeverity = comment.severity; 
      const originalSeverityEnum = this.getSeverityEnumValue(originalSeverity);
      
      if (originalSeverityEnum === newSeverity) {
        return;
      }
      
      comment.severity = newSeverity;
      this.commentsService.updateCommentSeverity(this.reviewId, commentId, newSeverity).subscribe({
        next: (response) => {
        },
        error: (error) => {
          comment.severity = originalSeverity;
          this.messageService.add({ 
            severity: 'error', 
            icon: 'bi bi-exclamation-triangle', 
            summary: 'Update Failed', 
            detail: `Failed to update comment severity. Server error: ${error.status || 'Unknown'}. Please try again.`, 
            key: 'bc', 
            life: 5000 
          });
          // Force UI update to show reverted value
          this.changeDetectorRef.detectChanges();
        }
      });
    } else if (!this.reviewId || this.reviewId.trim() === '') {
      this.messageService.add({ 
        severity: 'warn', 
        icon: 'bi bi-exclamation-triangle', 
        summary: 'Update Not Available', 
        detail: 'Cannot update severity: review information is not available.', 
        key: 'bc', 
        life: 3000 
      });
    }
  }

  startEditingSeverity(commentId: string): void {
    this.isEditingSeverity = commentId;
    this.changeDetectorRef.detectChanges();
  }

  stopEditingSeverity(): void {
    this.isEditingSeverity = null;
    this.changeDetectorRef.detectChanges();
  }

  hasSeverity(severity: CommentSeverity | null | undefined): boolean {
    return severity !== null && severity !== undefined;
  }

  onSeveritySelectionChange(newSeverity: CommentSeverity): void {
    this.selectedSeverity = newSeverity;
    
    if (newSeverity === CommentSeverity.Question || newSeverity === CommentSeverity.Suggestion) {
      this.allowAnyOneToResolve = true;  // Questions and Suggestions can be resolved by anyone
    } else if (newSeverity === CommentSeverity.ShouldFix || newSeverity === CommentSeverity.MustFix) {
      this.allowAnyOneToResolve = false; // These need more restricted resolution
    }
    
    this.changeDetectorRef.detectChanges();
  }

  getRelatedCommentsCount(comment: CommentItemModel): number {
    return CommentRelationHelper.getRelatedCommentsCount(comment, this.allComments, this.allCodePanelRowData);
  }

  hasRelatedComments(comment: CommentItemModel): boolean {
    return CommentRelationHelper.hasRelatedComments(comment, this.allComments, this.allCodePanelRowData);
  }

  showRelatedComments(comment: CommentItemModel) {
    if (!comment.correlationId || !this.allComments) {
      return;
    }

    this.selectedCommentId = comment.id;
    const cacheKey = comment.correlationId;
    if (this.visibleRelatedCommentsCache.has(cacheKey)) {
      this.relatedComments = this.visibleRelatedCommentsCache.get(cacheKey)!;
    } else {
      if (this.allCodePanelRowData && this.allCodePanelRowData.length > 0) {
        this.relatedComments = CommentRelationHelper.getVisibleRelatedComments(comment, this.allComments, this.allCodePanelRowData);
      } else {
        this.relatedComments = CommentRelationHelper.getRelatedComments(comment, this.allComments);
      }
      this.visibleRelatedCommentsCache.set(cacheKey, this.relatedComments);
    }
    
    this.showRelatedCommentsDialog = true;
  }

  onResolveSelectedComments(commentIds: string[]) {
    commentIds.forEach(commentId => {
      const comment = this.relatedComments.find(c => c.id === commentId);
      if (comment) {
        const commentCodeRow = this.allCodePanelRowData?.find(row => row.nodeId === comment.elementId);
        
        this.commentResolutionActionEmitter.emit({
          commentThreadUpdateAction: CommentThreadUpdateAction.CommentResolved,
          elementId: comment.elementId,
          nodeIdHashed: commentCodeRow?.nodeIdHashed ?? this.codePanelRowData?.nodeIdHashed,
          associatedRowPositionInGroup: commentCodeRow?.associatedRowPositionInGroup ?? this.codePanelRowData?.associatedRowPositionInGroup,
          resolvedBy: this.userProfile?.userName,
          commentId: commentId
        } as CommentUpdatesDto);
      }
    });
    this.showRelatedCommentsDialog = false;
  }
}
