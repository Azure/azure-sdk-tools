import { Component, EventEmitter, Input, Output, QueryList, SimpleChanges, ViewChildren } from '@angular/core';
import { MenuItem, MenuItemCommandEvent } from 'primeng/api';
import { Menu } from 'primeng/menu';
import { UserProfileService } from 'src/app/_services/user-profile/user-profile.service';
import { environment } from 'src/environments/environment';
import { EditorComponent } from '../editor/editor.component';
import { CodePanelRowData } from 'src/app/_models/codePanelModels';
import { UserProfile } from 'src/app/_models/userProfile';

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
  @Output() cancelCommentActionEmitter : EventEmitter<any> = new EventEmitter<any>();
  @Output() saveCommentActionEmitter : EventEmitter<any> = new EventEmitter<any>();
  @Output() deleteCommentActionEmitter : EventEmitter<any> = new EventEmitter<any>();
  @Output() commentResolutionActionEmitter : EventEmitter<any> = new EventEmitter<any>();
  @Output() commentUpvoteActionEmitter : EventEmitter<any> = new EventEmitter<any>();

  @ViewChildren(Menu) menus!: QueryList<Menu>;
  @ViewChildren(EditorComponent) editor!: QueryList<EditorComponent>;
  
  userProfile : UserProfile | undefined;
  commentEditText: string | undefined;
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

  constructor(private userProfileService: UserProfileService) { }

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
          state: {
            repo: "azure-sdk-for-net",
            language: "C#"
          }
        },
        {
          title: "java",
          label: "Java",
        },
        {
          title: "python",
          label: "Python",
        },
        {
          title: "c",
          label: "C",
        },
        {
          title: "javascript",
          label: "JavaScript",
        },
        {
          title: "go",
          label: "Go",
        },
        {
          title: "cplusplus",
          label: "C++",
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
      this.threadResolvedBy = this.codePanelRowData?.commentThreadIsResolvedBy ?? this.codePanelRowData?.comments?.find(comment => comment.isResolved)?.changeHistory.find(ch => ch.changeAction === 'resolved')?.changedBy;
      this.spacingBasedOnResolvedState = 'mb-2';
      this.resolveThreadButtonText = 'Unresolve';
    }
    else {
      this.threadResolvedBy = '';
      this.spacingBasedOnResolvedState = 'my-2';
      this.resolveThreadButtonText = 'Resolve';
    }
  }

  getCommentActionMenuContent(commentId: string) {
    const comment = this.codePanelRowData!.comments?.find(comment => comment.id === commentId);
    const menu : MenuItem[] = [];
    if (comment && this.userProfile?.userName === comment.createdBy) {
      menu.push(...this.menuItemsLoggedInUsers);
    }
    menu.push(...this.menuItemAllUsers);
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

  createGitHubIsuue(title : string) {
    let repo = "";
    switch (title) {
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
  }

  showReplyEditor(event: Event) {
    this.codePanelRowData!.showReplyTextBox = true;
  }

  deleteComment(event: MenuItemCommandEvent) {
    const target = (event.originalEvent?.target as Element).closest("a") as Element;
    const commentId = target.getAttribute("data-item-id");
    this.deleteCommentActionEmitter.emit({ 
      nodeIdHashed: this.codePanelRowData!.nodeIdHashed,
      commentId: commentId,
      associatedRowPositionInGroup: this.codePanelRowData!.associatedRowPositionInGroup});
  }

  showEditEditor = (event: MenuItemCommandEvent) => {
    const target = (event.originalEvent?.target as Element).closest("a") as Element;
    const commentId = target.getAttribute("data-item-id");
    this.codePanelRowData!.comments!.find(comment => comment.id === commentId)!.isInEditMode = true;
  }

  cancelCommentAction(event: Event) {
    const target = event.target as Element;
    const replyEditorContainer = target.closest(".reply-editor-container") as Element;
    if (replyEditorContainer) {
      this.codePanelRowData!.showReplyTextBox = false;
      this.cancelCommentActionEmitter.emit(
        {
          nodeIdHashed: this.codePanelRowData!.nodeIdHashed,
          associatedRowPositionInGroup: this.codePanelRowData!.associatedRowPositionInGroup
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

    if (replyEditorContainer) {
      const replyEditor = this.editor.find(e => e.editorId === "replyEditor");
      const content = replyEditor?.getEditorContent();
      this.saveCommentActionEmitter.emit(
        { 
          nodeId: this.codePanelRowData!.nodeId,
          nodeIdHashed: this.codePanelRowData!.nodeIdHashed,
          commentText: content,
          allowAnyOneToResolve: this.allowAnyOneToResolve,
          associatedRowPositionInGroup: this.codePanelRowData!.associatedRowPositionInGroup
        }
      );
      this.codePanelRowData!.showReplyTextBox = false;
    } else {
      const panel = target.closest("p-panel") as Element;
      const commentId = panel.getAttribute("data-comment-id");
      const replyEditor = this.editor.find(e => e.editorId === commentId);
      const content = replyEditor?.getEditorContent();
      this.saveCommentActionEmitter.emit(
        { 
          nodeId: this.codePanelRowData!.nodeId,
          nodeIdHashed: this.codePanelRowData!.nodeIdHashed,
          commentId: commentId,
          commentText: content,
          associatedRowPositionInGroup: this.codePanelRowData!.associatedRowPositionInGroup
        }
      );
      this.codePanelRowData!.comments!.find(comment => comment.id === commentId)!.isInEditMode = false;
    }
  }

  toggleUpVoteAction(event: Event) {
    const target = (event.target as Element).closest("button") as Element;
    const commentId = target.getAttribute("data-btn-id");
    this.commentUpvoteActionEmitter.emit(
      { 
        nodeIdHashed: this.codePanelRowData!.nodeIdHashed,
        commentId: commentId,
        associatedRowPositionInGroup: this.codePanelRowData!.associatedRowPositionInGroup
      }
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
  }

  handleThreadResolutionButtonClick(action: string) {
    this.commentResolutionActionEmitter.emit(
      { 
        elementId: this.codePanelRowData!.comments[0].elementId,
        action: action,
        nodeIdHashed: this.codePanelRowData!.nodeIdHashed,
        associatedRowPositionInGroup: this.codePanelRowData!.associatedRowPositionInGroup
      }
    );
  }
}
