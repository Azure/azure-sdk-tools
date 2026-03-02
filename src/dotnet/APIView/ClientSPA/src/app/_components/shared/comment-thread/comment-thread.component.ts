import { ChangeDetectorRef, Component, EventEmitter, Input, Output, QueryList, SimpleChanges, ViewChildren, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MenuItem, MenuItemCommandEvent, MessageService } from 'primeng/api';
import { Menu, MenuModule } from 'primeng/menu';
import { Popover, PopoverModule } from 'primeng/popover';
import { PanelModule } from 'primeng/panel';
import { TimelineModule } from 'primeng/timeline';
import { TooltipModule } from 'primeng/tooltip';
import { SelectModule } from 'primeng/select';
import { TimeagoModule } from 'ngx-timeago';
import { take } from 'rxjs/operators';
import { environment } from 'src/environments/environment';
import { EditorComponent } from '../editor/editor.component';
import { RelatedCommentsDialogComponent } from '../related-comments-dialog/related-comments-dialog.component';
import { AICommentFeedbackDialogComponent } from '../ai-comment-feedback-dialog/ai-comment-feedback-dialog.component';
import { AICommentDeleteDialogComponent } from '../ai-comment-delete-dialog/ai-comment-delete-dialog.component';
import { MarkdownToHtmlPipe } from 'src/app/_pipes/markdown-to-html.pipe';
import { LanguageNamesPipe } from 'src/app/_pipes/language-names.pipe';
import { CodePanelRowData } from 'src/app/_models/codePanelModels';
import { UserProfile } from 'src/app/_models/userProfile';
import { CommentThreadUpdateAction, CommentUpdatesDto } from 'src/app/_dtos/commentThreadUpdateDto';
import { CodeLineRowNavigationDirection } from 'src/app/_helpers/common-helpers';
import { CommentSeverityHelper } from 'src/app/_helpers/comment-severity.helper';
import { CommentSeverity, CommentSource } from 'src/app/_models/commentItemModel';
import { CommentsService } from 'src/app/_services/comments/comments.service';
import { PermissionsService } from 'src/app/_services/permissions/permissions.service';
import { ReviewContextService } from 'src/app/_services/review-context/review-context.service';
import { CommentItemModel } from 'src/app/_models/commentItemModel';
import { CommentRelationHelper } from 'src/app/_helpers/comment-relation.helper';
import { CommentResolutionData } from '../related-comments-dialog/related-comments-dialog.component';
import { AICommentFeedback } from '../ai-comment-feedback-dialog/ai-comment-feedback-dialog.component';
import { AICommentDeleteReason } from '../ai-comment-delete-dialog/ai-comment-delete-dialog.component';

interface AICommentInfoItem {
  icon: string;
  label: string;
  value: string;
  valueList?: string[];
  valueClass?: string;
}

interface AICommentInfo {
  items: AICommentInfoItem[];
}
@Component({
    selector: 'app-comment-thread',
    templateUrl: './comment-thread.component.html',
    styleUrls: ['./comment-thread.component.scss'],
    host: {
        'class': 'user-comment-content'
    },
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        MenuModule,
        PopoverModule,
        PanelModule,
        TimelineModule,
        TooltipModule,
        SelectModule,
        TimeagoModule,
        EditorComponent,
        RelatedCommentsDialogComponent,
        AICommentFeedbackDialogComponent,
        AICommentDeleteDialogComponent,
        MarkdownToHtmlPipe,
        LanguageNamesPipe
    ]
})
export class CommentThreadComponent {
  @Input() codePanelRowData: CodePanelRowData | undefined = undefined;
  @Input() associatedCodeLine: CodePanelRowData | undefined;
  @Input() actualLineNumber: number = 0;
  @Input() instanceLocation: "code-panel" | "conversations" | "samples" = "code-panel";
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
  @Output() commentThreadNavigationEmitter : EventEmitter<any> = new EventEmitter<any>();
  @Output() batchResolutionActionEmitter : EventEmitter<CommentUpdatesDto> = new EventEmitter<CommentUpdatesDto>();

  @ViewChildren(Menu) menus!: QueryList<Menu>;
  @ViewChildren(EditorComponent) editor!: QueryList<EditorComponent>;
  @ViewChild('aiInfoPanel') aiInfoPanel!: Popover;

  assetsPath : string = environment.assetsPath;
  currentAIInfoStructured: AICommentInfo | null = null;
  menuItemsGitHubIssue: MenuItem[] = [];
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
  severityOptions = CommentSeverityHelper.severityOptions;

  showRelatedCommentsDialog: boolean = false;
  relatedComments: CommentItemModel[] = [];
  selectedCommentId: string = '';

  showAIFeedbackDialog: boolean = false;
  showAIDeleteDialog: boolean = false;
  pendingDownvoteAction: CommentUpdatesDto | null = null;
  pendingDeleteAction: CommentUpdatesDto | null = null;

  get pendingDownvoteCommentId(): string {
    return this.pendingDownvoteAction?.commentId || '';
  }

  get pendingDeleteCommentId(): string {
    return this.pendingDeleteAction?.commentId || '';
  }

  private visibleRelatedCommentsCache = new Map<string, CommentItemModel[]>();

  get canEditSeverity(): boolean {
    if (!this.codePanelRowData?.comments || this.codePanelRowData.comments.length === 0) {
      return false;
    }

    const firstComment = this.codePanelRowData.comments[0];
    return firstComment.createdBy === this.userProfile?.userName ||
           this.permissionsService.isAdmin(this.userProfile?.permissions) ||
           (firstComment.createdBy === 'azure-sdk' && this.permissionsService.isApproverFor(this.userProfile?.permissions, this.reviewContextService.getLanguage()));
  }

  CodeLineRowNavigationDirection = CodeLineRowNavigationDirection;

  constructor(private changeDetectorRef: ChangeDetectorRef, private messageService: MessageService, private commentsService: CommentsService, private permissionsService: PermissionsService, private reviewContextService: ReviewContextService) { }

  ngOnInit(): void {
    this.menuItemsGitHubIssue.push({
      label: 'Create GitHub Issue',
      items: [
        { title: "c", label: "C", command: (event) => this.createGitHubIssue(event) },
        { title: "cplusplus", label: "C++", command: (event) => this.createGitHubIssue(event) },
        { title: "go", label: "Go", command: (event) => this.createGitHubIssue(event) },
        { title: "java", label: "Java", command: (event) => this.createGitHubIssue(event) },
        { title: "javascript", label: "JavaScript", command: (event) => this.createGitHubIssue(event) },
        { title: "csharp", label: ".NET", command: (event) => this.createGitHubIssue(event) },
        { title: "python", label: "Python", command: (event) => this.createGitHubIssue(event) },
        { title: "rust", label: "Rust", command: (event) => this.createGitHubIssue(event) },
        { title: "apiview", label: "APIView", command: (event) => this.createGitHubIssue(event) },
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
    const menu: MenuItem[] = [];

    // Copy link in its own section
    menu.push({ items: [
      { label: 'Copy link', icon: 'pi pi-link', command: (event) => this.copyCommentLink(event) },
    ]});

    // Add edit/delete for comment owners (non-system-generated comments)
    if (comment && this.userProfile?.userName === comment.createdBy && !this.isSystemGenerated(comment)) {
      menu.push({ separator: true });
      menu.push({ items: [
        { label: 'Edit', icon: 'pi pi-pencil', command: (event) => this.showEditEditor(event) },
        { label: 'Delete', icon: 'pi pi-trash', command: (event) => this.deleteComment(event) }
      ]});
    } else if (comment && this.permissionsService.isAdmin(this.userProfile?.permissions)) {
      // Admins can delete any comment but not edit others' comments
      menu.push({ separator: true });
      menu.push({ items: [
        { label: 'Delete', icon: 'pi pi-trash', command: (event) => this.deleteComment(event) }
      ]});
    } else if (comment && comment.createdBy == "azure-sdk" && this.permissionsService.isApproverFor(this.userProfile?.permissions, this.reviewContextService.getLanguage())) {
      menu.push({ separator: true });
      menu.push({ items: [
        { label: 'Delete', icon: 'pi pi-trash', command: (event) => this.deleteComment(event) }
      ]});
    }

    // Add GitHub Issue submenu (except for samples)
    if (this.instanceLocation !== "samples") {
      menu.push({ separator: true });
      menu.push(...this.menuItemsGitHubIssue);
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
      case "c":
        repo = "azure-sdk-for-c";
        break;
      case "cplusplus":
        repo = "azure-sdk-for-cpp";
        break;
      case "go":
        repo = "azure-sdk-for-go";
        break;
      case "java":
        repo = "azure-sdk-for-java";
        break;
      case "javascript":
        repo = "azure-sdk-for-js";
        break;
      case "csharp":
        repo = "azure-sdk-for-net";
        break;
      case "python":
        repo = "azure-sdk-for-python";
        break;
      case "rust":
        repo = "azure-sdk-for-rust";
        break;
      case "apiview":
        repo = "azure-sdk-tools";
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

  copyCommentLink(event: MenuItemCommandEvent) {
    const target = (event.originalEvent?.target as Element)?.closest("a");
    if (!target) {
      return;
    }

    const commentId = target.getAttribute("data-item-id");
    if (!commentId) {
      this.messageService.add({ severity: 'error', summary: 'Copy failed', detail: 'Unable to find comment ID', life: 3000 });
      return;
    }

    const comment = this.codePanelRowData?.comments?.find(c => c.id === commentId);

    const nodeId: string = comment?.elementId || this.codePanelRowData?.nodeId || '';

    // Build URL that always points to the review page, preserving revision parameters
    const baseUrl = window.location.origin + window.location.pathname.replace(/\/(conversation|samples)$/, '');
    const queryParams = new URLSearchParams(window.location.search);
    if (nodeId) {
      queryParams.set('nId', nodeId);
    }
    const queryString = queryParams.toString();
    const commentUrl = `${baseUrl}${queryString ? '?' + queryString : ''}#${commentId}`;

    navigator.clipboard.writeText(commentUrl).then(() => {
      this.messageService.add({ severity: 'success', summary: 'Link copied', detail: 'Comment link copied to clipboard', life: 3000 });
    }).catch(() => {
      this.messageService.add({ severity: 'error', summary: 'Copy failed', detail: 'Unable to copy link to clipboard', life: 3000 });
    });
  }

  showReplyEditor(event: Event) {
    if (this.codePanelRowData!.draftCommentText === undefined) {
      this.codePanelRowData!.draftCommentText = '';
    }
    this.codePanelRowData!.showReplyTextBox = true;
  }

  deleteComment(event: MenuItemCommandEvent) {
    const target = (event.originalEvent?.target as Element).closest("a") as Element;
    const commentId = target.getAttribute("data-item-id");
    const title = target.getAttribute("data-element-id");

    const deleteAction = {
      commentThreadUpdateAction: CommentThreadUpdateAction.CommentDeleted,
      nodeIdHashed: this.codePanelRowData!.nodeIdHashed,
      threadId: this.codePanelRowData!.threadId,
      commentId: commentId,
      associatedRowPositionInGroup: this.codePanelRowData!.associatedRowPositionInGroup,
      title: title // Used for Sample Instance of CommentThread
    } as CommentUpdatesDto;

    const comment = this.codePanelRowData?.comments?.find(c => c.id === commentId);

    if (comment?.commentSource === CommentSource.AIGenerated) {
      this.pendingDeleteAction = deleteAction;
      setTimeout(() => {
        this.showAIDeleteDialog = true;
        this.changeDetectorRef.detectChanges();
      }, 100);
    } else {
      this.deleteCommentActionEmitter.emit(deleteAction);
    }
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
      this.codePanelRowData!.draftCommentText = '';
      this.selectedSeverity = null;
      this.cancelCommentActionEmitter.emit(
        {
          nodeIdHashed: this.codePanelRowData!.nodeIdHashed,
          associatedRowPositionInGroup: this.codePanelRowData!.associatedRowPositionInGroup,
          threadId: this.codePanelRowData!.threadId,
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
    } else if (this.instanceLocation === "code-panel") {
      if (this.codePanelRowData?.comments && this.codePanelRowData.comments.length > 0) {
        elementId = this.codePanelRowData.comments[0].elementId;
      } else {
        elementId = this.codePanelRowData?.nodeId;
      }
    }

    const HTML_STRIP_REGEX = /<\/?[^>]+(>|$)/g
    const emptyCommentContentWarningMessage = { severity: 'info', icon: 'bi bi-info-circle', summary: "Comment Info", detail: "Comment content is empty. No action taken.", key: 'bc', life: 3000 };

    const nodeIdValue = this.codePanelRowData?.nodeId || elementId;
    const elementIdValue = elementId || this.codePanelRowData?.nodeId;

    if (replyEditorContainer) {
      const content = this.getEditorContent("replyEditor");
      const contentText = content.replace(HTML_STRIP_REGEX, '');
      if (contentText.length === 0) {
        this.messageService.add(emptyCommentContentWarningMessage);
      } else {
        // For replies (thread already has comments), severity and resolution settings
        // are owned by the thread starter and must not be overridden by replies.
        const isReply = this.codePanelRowData!.comments && this.codePanelRowData!.comments.length > 0;
        this.saveCommentActionEmitter.emit(
          {
            commentThreadUpdateAction: CommentThreadUpdateAction.CommentCreated,
            nodeId: nodeIdValue,
            nodeIdHashed: this.codePanelRowData!.nodeIdHashed,
            threadId: this.codePanelRowData!.threadId,
            commentText: content,
            allowAnyOneToResolve: isReply ? undefined : this.allowAnyOneToResolve,
            associatedRowPositionInGroup: this.codePanelRowData!.associatedRowPositionInGroup,
            elementId: elementIdValue,
            revisionId: revisionIdForConversationGroup,
            severity: isReply ? null : this.selectedSeverity,
            isReply: isReply
          } as CommentUpdatesDto
        );
        this.selectedSeverity = null;
        this.codePanelRowData!.showReplyTextBox = false;
        this.codePanelRowData!.draftCommentText = '';
      }
    } else {
      const panel = target.closest("p-panel") as Element;
      const commentId = panel.getAttribute("data-comment-id");
      const content = this.getEditorContent(commentId!);
      const contentText = content.replace(HTML_STRIP_REGEX, '');
      if (contentText.length === 0) {
        this.messageService.add(emptyCommentContentWarningMessage);
      } else {
        const comment = this.codePanelRowData!.comments!.find(comment => comment.id === commentId)!;
        comment.commentText = content;
        comment.isInEditMode = false;
        this.saveCommentActionEmitter.emit(
          {
            commentThreadUpdateAction: CommentThreadUpdateAction.CommentTextUpdate,
            nodeId: nodeIdValue,
            nodeIdHashed: this.codePanelRowData!.nodeIdHashed,
            threadId: this.codePanelRowData!.threadId,
            commentId: commentId,
            commentText: content,
            associatedRowPositionInGroup: this.codePanelRowData!.associatedRowPositionInGroup,
            elementId: elementIdValue,
            revisionId: revisionIdForConversationGroup
          } as CommentUpdatesDto
        );
      }
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
        threadId: this.codePanelRowData!.threadId,
        commentId: commentId,
        associatedRowPositionInGroup: this.codePanelRowData!.associatedRowPositionInGroup
      } as CommentUpdatesDto
    );
  }

  toggleDownVoteAction(event: Event) {
    const target = (event.target as Element).closest("button") as Element;
    const commentId = target.getAttribute("data-btn-id");
    const comment = this.codePanelRowData?.comments?.find(c => c.id === commentId);
    const isAIComment = comment?.commentSource === CommentSource.AIGenerated;
    const hasDownvote = comment?.downvotes?.includes(this.userProfile?.userName || '');

    const downvoteAction = {
      commentThreadUpdateAction: CommentThreadUpdateAction.CommentDownVoteToggled,
      nodeIdHashed: this.codePanelRowData!.nodeIdHashed,
      threadId: this.codePanelRowData!.threadId,
      commentId: commentId,
      associatedRowPositionInGroup: this.codePanelRowData!.associatedRowPositionInGroup
    } as CommentUpdatesDto;

    if (isAIComment && !hasDownvote) {
      this.pendingDownvoteAction = downvoteAction;
      setTimeout(() => {
        this.showAIFeedbackDialog = true;
        this.changeDetectorRef.detectChanges();
      }, 100);
    } else {
      this.commentDownvoteActionEmitter.emit(downvoteAction);
    }
  }

  onAIFeedbackSubmit(feedback: AICommentFeedback): void {
    this.showAIFeedbackDialog = false;

    if (this.pendingDownvoteAction) {
      this.commentDownvoteActionEmitter.emit(this.pendingDownvoteAction);
    }

    if (feedback.reasons.length > 0) {
      this.commentsService.submitAICommentFeedback(
        this.reviewId,
        feedback.commentId,
        feedback.reasons,
        feedback.additionalComments,
        false
      ).pipe(take(1)).subscribe({
        next: () => {
        },
        error: (error: any) => {
          console.error('Failed to submit feedback:', error);
        }
      });
    }

    this.pendingDownvoteAction = null;
  }

  onAIFeedbackCancel(): void {
    this.showAIFeedbackDialog = false;
    this.pendingDownvoteAction = null;
  }

  onAIDeleteConfirm(deleteReason: AICommentDeleteReason): void {
    this.showAIDeleteDialog = false;

    if (this.pendingDeleteAction) {
      this.deleteCommentActionEmitter.emit(this.pendingDeleteAction);
    }

    if (deleteReason.reason.trim().length > 0) {
      this.commentsService.submitAICommentFeedback(
        this.reviewId,
        deleteReason.commentId,
        [],
        deleteReason.reason,
        true
      ).pipe(take(1)).subscribe({
        next: () => {
        },
        error: (error: any) => {
          console.error('Failed to submit deletion reason:', error);
        }
      });
    }

    this.pendingDeleteAction = null;
  }

  onAIDeleteCancel(): void {
    this.showAIDeleteDialog = false;
    this.pendingDeleteAction = null;
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
    const isResolving = action === "Resolve";
    if (isResolving) {
      this.threadResolvedBy = this.userProfile?.userName;
      // Collapse the thread when resolving
      this.threadResolvedAndExpanded = false;
      this.threadResolvedStateToggleText = 'Show';
      this.threadResolvedStateToggleIcon = 'bi-arrows-expand';
    }

    this.commentResolutionActionEmitter.emit(
      {
        commentThreadUpdateAction: isResolving ? CommentThreadUpdateAction.CommentResolved : CommentThreadUpdateAction.CommentUnResolved,
        elementId: this.codePanelRowData!.comments[0].elementId,
        threadId: this.codePanelRowData!.threadId,
        nodeIdHashed: this.codePanelRowData!.nodeIdHashed,
        associatedRowPositionInGroup: this.codePanelRowData!.associatedRowPositionInGroup,
        resolvedBy: this.userProfile?.userName
      } as CommentUpdatesDto
    );
  }

  handleCommentThreadNavigation(event: Event, direction: CodeLineRowNavigationDirection) {
    const target = (event.target as Element).closest(".user-comment-thread")?.parentNode as Element;
    const targetIndex = target.getAttribute("data-sid");
    this.commentThreadNavigationEmitter.emit({
      commentThreadNavigationPointer: targetIndex,
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


  getSeverityLabel(severity: CommentSeverity | string | null | undefined): string {
    return CommentSeverityHelper.getSeverityLabel(severity);
  }

  getSeverityBadgeClass(severity: CommentSeverity | string | null | undefined): string {
    return CommentSeverityHelper.getSeverityBadgeClass(severity);
  }

  getSeverityEnumValue(severity: CommentSeverity | string | null | undefined): CommentSeverity | null {
    return CommentSeverityHelper.getSeverityEnumValue(severity);
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
          this.commentsService.notifySeverityChanged(commentId, newSeverity);
          this.commentsService.notifyQualityScoreRefresh();
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

  onResolveSelectedComments(resolutionData: CommentResolutionData) {
    const { commentIds, batchVote, resolutionComment, disposition, severity, feedbackReasons, feedbackAdditionalComments } = resolutionData;

    if (commentIds.length === 0) {
      this.showRelatedCommentsDialog = false;
      return;
    }

    const hasFeedbackReasons = feedbackReasons && feedbackReasons.length > 0;
    const hasFeedbackComment = feedbackAdditionalComments && feedbackAdditionalComments.trim().length > 0;

    const feedback = (hasFeedbackReasons || hasFeedbackComment) ? {
      reasons: feedbackReasons || [],
      comment: feedbackAdditionalComments || '',
      isDelete: disposition === 'delete'
    } : undefined;

    this.commentsService.commentsBatchOperation(this.reviewId, {
      commentIds: commentIds,
      vote: batchVote || 'none',
      commentReply: resolutionComment || undefined,
      disposition: disposition,
      severity: severity,
      feedback: feedback
    }).subscribe({
      next: (response) => {
        const createdComments = response.body || [];

        if (severity !== null && severity !== undefined) {
          this.applyBatchSeverity(commentIds, severity);
        }

        // Only emit resolution events if disposition is 'resolve'
        if (disposition === 'resolve') {
          this.emitResolutionEvents(commentIds);
        }

        this.emitCreationEvents(createdComments);

        // Refresh quality score after batch operation completes
        this.commentsService.notifyQualityScoreRefresh();

        this.showRelatedCommentsDialog = false;
      },
      error: (error) => {
        console.error('Failed to resolve batch comments:', error);
        this.messageService.add({
          severity: 'error',
          summary: 'Resolution Failed',
          detail: 'Failed to resolve comments. Please try again.',
          key: 'bc',
          life: 5000
        });
      }
    });
  }

  private applyBatchSeverity(commentIds: string[], severity: CommentSeverity): void {
    commentIds.forEach(commentId => {
      const commentCodeRow = this.allCodePanelRowData?.find(row =>
        row.comments?.some(c => c.id === commentId)
      );

      if (!commentCodeRow) {
        return;
      }

      const comment = commentCodeRow.comments?.find(c => c.id === commentId);
      if (comment) {
        comment.severity = severity;
      }

      const relatedComment = this.relatedComments?.find(c => c.id === commentId);
      if (relatedComment) {
        relatedComment.severity = severity;
      }

      this.batchResolutionActionEmitter.emit({
        commentThreadUpdateAction: CommentThreadUpdateAction.CommentTextUpdate,
        commentId: commentId,
        threadId: comment?.threadId || commentCodeRow.threadId || this.codePanelRowData?.threadId,
        nodeIdHashed: commentCodeRow.nodeIdHashed,
        severity: severity,
        associatedRowPositionInGroup: commentCodeRow.associatedRowPositionInGroup || 0
      } as CommentUpdatesDto);
    });
  }

  private emitResolutionEvents(commentIds: string[]): void {
    commentIds.forEach(commentId => {
      const comment = this.relatedComments.find(c => c.id === commentId);
      if (comment) {
        const commentCodeRow = this.allCodePanelRowData?.find(row =>
          row.threadId === comment.threadId || row.comments?.some(c => c.id === commentId)
        );

        this.batchResolutionActionEmitter.emit({
          commentThreadUpdateAction: CommentThreadUpdateAction.CommentResolved,
          elementId: comment.elementId,
          commentId: comment.id,
          threadId: comment.threadId || commentCodeRow?.threadId || this.codePanelRowData?.threadId,
          nodeIdHashed: commentCodeRow?.nodeIdHashed ?? this.codePanelRowData?.nodeIdHashed,
          associatedRowPositionInGroup: commentCodeRow?.associatedRowPositionInGroup ?? this.codePanelRowData?.associatedRowPositionInGroup,
          resolvedBy: this.userProfile?.userName
        } as CommentUpdatesDto);
      }
    });
  }

  private emitCreationEvents(createdComments: CommentItemModel[]): void {
    createdComments.forEach(createdComment => {
      const originalComment = this.relatedComments.find(rc => rc.elementId === createdComment.elementId);

      let commentCodeRow: CodePanelRowData | undefined;
      if (originalComment) {
        commentCodeRow = this.allCodePanelRowData?.find(row =>
          row.comments?.some(c => c.id === originalComment.id)
        );
      }

      if (!commentCodeRow) {
        commentCodeRow = this.allCodePanelRowData?.find(row =>
          (createdComment.threadId && row.threadId === createdComment.threadId) ||
          row.nodeId === createdComment.elementId
        );
      }

      this.batchResolutionActionEmitter.emit({
        commentThreadUpdateAction: CommentThreadUpdateAction.CommentCreated,
        comment: createdComment,
        elementId: createdComment.elementId,
        threadId: createdComment.threadId || commentCodeRow?.threadId || this.codePanelRowData?.threadId,
        nodeIdHashed: commentCodeRow?.nodeIdHashed,
        associatedRowPositionInGroup: commentCodeRow?.associatedRowPositionInGroup,
        reviewId: this.reviewId
      } as CommentUpdatesDto);
    });
  }

  isAIGenerated(comment: CommentItemModel): boolean {
    return comment.commentSource === CommentSource.AIGenerated;
  }

  isDiagnostic(comment: CommentItemModel): boolean {
    return comment.commentSource === CommentSource.Diagnostic;
  }

  isSystemGenerated(comment: CommentItemModel): boolean {
    return this.isAIGenerated(comment) || this.isDiagnostic(comment);
  }

  hasAIInfo(comment: CommentItemModel): boolean {
    if (!this.isAIGenerated(comment)) {
      return false;
    }
    const info = this.getAICommentInfoStructured(comment);
    return info.items.length > 0;
  }

  getAICommentInfoStructured(comment: CommentItemModel): AICommentInfo {
    const items: AICommentInfoItem[] = [];

    if (comment.confidenceScore && comment.confidenceScore > 0) {
      const score = Math.round(comment.confidenceScore * 100);
      const scoreClass = score >= 80 ? 'high-confidence' : score >= 60 ? 'medium-confidence' : 'low-confidence';
      items.push({
        icon: 'pi-chart-bar',
        label: 'Confidence Score',
        value: `${score}%`,
        valueClass: scoreClass
      });
    }

    if (comment.guidelineIds && comment.guidelineIds.length > 0) {
      items.push({
        icon: 'pi-book',
        label: 'Guidelines Referenced',
        value: '',
        valueList: comment.guidelineIds
      });
    }

    if (comment.memoryIds && comment.memoryIds.length > 0) {
      items.push({
        icon: 'pi-database',
        label: 'Memory References',
        value: '',
        valueList: comment.memoryIds
      });
    }

    items.push({
        icon: 'pi-id-card',
        label: 'Id',
        value: comment.id,
      });

    return { items };
  }

  showAIInfo(event: Event, comment: CommentItemModel): void {
    this.currentAIInfoStructured = this.getAICommentInfoStructured(comment);
    this.aiInfoPanel.toggle(event);
  }
}
