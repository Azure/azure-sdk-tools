import { Component, ElementRef, Injector, Input, QueryList, Renderer2, ViewChild, ViewChildren, ViewContainerRef } from '@angular/core';
import { MenuItem, MenuItemCommandEvent } from 'primeng/api';
import { Menu } from 'primeng/menu';
import { UserProfile } from 'src/app/_models/auth_service_models';
import { CommentItemModel } from 'src/app/_models/review';
import { UserProfileService } from 'src/app/_services/user-profile/user-profile.service';
import { environment } from 'src/environments/environment';

@Component({
  selector: 'app-comment-thread',
  templateUrl: './comment-thread.component.html',
  styleUrls: ['./comment-thread.component.scss'],
  host: {
    'class': 'user-comment-content'
  },
})
export class CommentThreadComponent {
  @Input() comments: CommentItemModel[] | undefined = [];
  @ViewChildren(Menu) menus!: QueryList<Menu>;
  
  userProfile : UserProfile | undefined;
  commentEditText: string | undefined;
  assetsPath : string = environment.assetsPath;
  menuItemAllUsers: MenuItem[] = [];
  menuItemsLoggedInUsers: MenuItem[] = [];

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
        { label: 'Delete', icon: 'bi bi-trash', command: () => {} },
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
    if (true) { //(comment && this.userProfile?.userName === comment.createdBy)) {
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
    const target = event.target as Element;
    const replyButtonContainer = target.closest(".reply-button-container") as Element;
    const replyEditorContainer = (replyButtonContainer.parentElement as Element).previousSibling as Element;
    replyButtonContainer.classList.add("d-none");
    replyEditorContainer.classList.remove("d-none");
  }

  showEditEditor = (event: MenuItemCommandEvent) => {
    const target = (event.originalEvent?.target as Element).closest("a") as Element;
    const commentId = target.getAttribute("data-item-id");
    const commentPanel = document.querySelector(`p-panel[data-comment-id="${commentId}"]`) as Element;
    const renderedCommentContent = commentPanel.querySelector(".rendered-comment-content") as Element;
    const editEditor = commentPanel.querySelector(".edit-editor-container") as Element;
    renderedCommentContent.classList.add("d-none");
    editEditor.classList.remove("d-none");
  }
}
