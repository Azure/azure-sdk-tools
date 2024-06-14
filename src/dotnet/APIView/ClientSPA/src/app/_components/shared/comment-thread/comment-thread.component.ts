import { Component, EventEmitter, Input, Output, QueryList, ViewChildren } from '@angular/core';
import { MenuItem, MenuItemCommandEvent } from 'primeng/api';
import { Menu } from 'primeng/menu';
import { UserProfile } from 'src/app/_models/auth_service_models';
import { CommentItemModel } from 'src/app/_models/review';
import { UserProfileService } from 'src/app/_services/user-profile/user-profile.service';
import { environment } from 'src/environments/environment';
import { EditorComponent } from '../editor/editor.component';

@Component({
  selector: 'app-comment-thread',
  templateUrl: './comment-thread.component.html',
  styleUrls: ['./comment-thread.component.scss'],
  host: {
    'class': 'user-comment-content'
  },
})
export class CommentThreadComponent {
  @Input() nodeId: string = '';
  @Input() nodeIdHashed : string = '';
  @Input() comments: CommentItemModel[] | undefined = [];
  @Input() showReplyTextBox: boolean = false;
  @Output() cancelCommentActionEmitter : EventEmitter<string> = new EventEmitter<string>();
  @Output() saveCommentActionEmitter : EventEmitter<any> = new EventEmitter<any>();
  @Output() deleteCommentActionEmitter : EventEmitter<any> = new EventEmitter<any>();

  @ViewChildren(Menu) menus!: QueryList<Menu>;
  @ViewChildren(EditorComponent) editor!: QueryList<EditorComponent>;
  
  userProfile : UserProfile | undefined;
  commentEditText: string | undefined;
  assetsPath : string = environment.assetsPath;
  menuItemAllUsers: MenuItem[] = [];
  menuItemsLoggedInUsers: MenuItem[] = [];
  allowAnyOneToResolve : boolean = true;

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
  }

  getCommentActionMenuContent(commentId: string) {
    const comment = this.comments?.find(comment => comment.id === commentId);
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
    this.showReplyTextBox = true;
  }

  deleteComment(event: MenuItemCommandEvent) {
    const target = (event.originalEvent?.target as Element).closest("a") as Element;
    const commentId = target.getAttribute("data-item-id");
    this.deleteCommentActionEmitter.emit({ nodeIdHashed: this.nodeIdHashed, commentId: commentId });
  }

  showEditEditor = (event: MenuItemCommandEvent) => {
    const target = (event.originalEvent?.target as Element).closest("a") as Element;
    const commentId = target.getAttribute("data-item-id");
    this.comments!.find(comment => comment.id === commentId)!.isInEditMode = true;
  }

  cancelCommentAction(event: Event) {
    const target = event.target as Element;
    const replyEditorContainer = target.closest(".reply-editor-container") as Element;
    if (replyEditorContainer) {
      this.showReplyTextBox = false;
      this.cancelCommentActionEmitter.emit(this.nodeIdHashed);
    } else {
      const panel = target.closest("p-panel") as Element;
      const commentId = panel.getAttribute("data-comment-id");
      this.comments!.find(comment => comment.id === commentId)!.isInEditMode = false;
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
          nodeId: this.nodeId,
          nodeIdHashed: this.nodeIdHashed,
          commentText: content,
          allowAnyOneToResolve: this.allowAnyOneToResolve
        }
      );
      this.showReplyTextBox = false;
    } else {
      const panel = target.closest("p-panel") as Element;
      const commentId = panel.getAttribute("data-comment-id");
      const replyEditor = this.editor.find(e => e.editorId === commentId);
      const content = replyEditor?.getEditorContent();
      this.saveCommentActionEmitter.emit(
        { 
          nodeId: this.nodeId,
          nodeIdHashed: this.nodeIdHashed,
          commentId: commentId,
          commentText: content
        }
      );
      this.comments!.find(comment => comment.id === commentId)!.isInEditMode = false;
    }
  }
}
