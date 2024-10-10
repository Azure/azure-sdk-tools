import { ChangeDetectorRef, Component, EventEmitter, Input, Output, QueryList, SimpleChanges, ViewChildren } from '@angular/core';
import { MenuItem, MenuItemCommandEvent, MessageService } from 'primeng/api';
import { Menu } from 'primeng/menu';
import { UserProfileService } from 'src/app/_services/user-profile/user-profile.service';
import { environment } from 'src/environments/environment';
import { EditorComponent } from '../editor/editor.component';
import { CodePanelRowData } from 'src/app/_models/codePanelModels';
import { UserProfile } from 'src/app/_models/userProfile';
import { CommentThreadUpdateAction, CommentUpdatesDto } from 'src/app/_dtos/commentThreadUpdateDto';
import { CodeLineRowNavigationDirection } from 'src/app/_helpers/common-helpers';

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
  @Input() instanceLocation: "code-panel" | "conversations" | "samples" = "code-panel";
  @Output() cancelCommentActionEmitter : EventEmitter<any> = new EventEmitter<any>();
  @Output() saveCommentActionEmitter : EventEmitter<any> = new EventEmitter<any>();
  @Output() deleteCommentActionEmitter : EventEmitter<any> = new EventEmitter<any>();
  @Output() commentResolutionActionEmitter : EventEmitter<any> = new EventEmitter<any>();
  @Output() commentUpvoteActionEmitter : EventEmitter<any> = new EventEmitter<any>();
  @Output() commentThreadNavaigationEmitter : EventEmitter<any> = new EventEmitter<any>();

  @ViewChildren(Menu) menus!: QueryList<Menu>;
  @ViewChildren(EditorComponent) editor!: QueryList<EditorComponent>;
  
  userProfile : UserProfile | undefined;
  assetsPath : string = environment.assetsPath;
  menuItemAllUsers: MenuItem[] = [];
  menuItemsLoggedInUsers: MenuItem[] = [];
  allowAnyOneToResolve : boolean = true;

  threadResolvedBy : string | undefined = '';
  threadResolvedStateToggleText : string = 'Show';
  threadResolvedStateToggleIcon : string = 'bi-arrows-expand';
  threadResolvedAndExpanded : boolean = false;
  spacingBasedOnResolvedState: string = 'my-2';
  resolveThreadButtonText : string = 'Resolve';

  floatItemStart : string = ""
  floatItemEnd : string = ""

  CodeLineRowNavigationDirection = CodeLineRowNavigationDirection;

  constructor(private userProfileService: UserProfileService, private changeDetectorRef: ChangeDetectorRef, private messageService: MessageService) { }

  ngOnInit(): void {
    this.userProfileService.getUserProfile().subscribe(
      (userProfile : any) => {
        this.userProfile = userProfile;
      });

    this.menuItemsLoggedInUsers.push({
      label: '',
      items: [
        { label: 'Edit', icon: 'bi bi-pencil-square', command: (event) => this.showEditEditor(event) },
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

    this.setCommentResolutionState();
  }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['codePanelRowData']) {
      this.setCommentResolutionState();
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
    const emptyCommentContentWarning = "Comment content is empty. No action taken.";

    if (replyEditorContainer) {
      const content = this.getEditorContent("replyEditor");
      const contentText = content.replace(HTML_STRIP_REGEX, '');
      if (contentText.length === 0) {
        this.messageService.add({ severity: 'info', icon: 'bi bi-info-circle', detail: emptyCommentContentWarning, key: 'bl', life: 3000 });
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
            revisionId: revisionIdForConversationGroup
          } as CommentUpdatesDto
        );
      }
      this.codePanelRowData!.showReplyTextBox = false;
    } else {
      const panel = target.closest("p-panel") as Element;
      const commentId = panel.getAttribute("data-comment-id");
      const content = this.getEditorContent(commentId!);
      const contentText = content.replace(HTML_STRIP_REGEX, '');
      if (contentText.length === 0) {
        this.messageService.add({ severity: 'info', icon: 'bi bi-info-circle', detail: emptyCommentContentWarning, key: 'bl', life: 3000 });
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
}
