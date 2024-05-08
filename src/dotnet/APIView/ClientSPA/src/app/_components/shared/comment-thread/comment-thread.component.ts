import { Component, ElementRef, Injector, Input, QueryList, Renderer2, ViewChildren } from '@angular/core';
import { MenuItem } from 'primeng/api';
import { Menu } from 'primeng/menu';
import { UserProfile } from 'src/app/_models/auth_service_models';
import { CommentItemModel } from 'src/app/_models/review';
import { AuthService } from 'src/app/_services/auth/auth.service';
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

  constructor(private authService: AuthService) { }

  ngOnInit(): void {
    this.authService.getUserProfile().subscribe(
      (userProfile : any) => {
        this.userProfile = userProfile;
      });
  }

  getCommentActionMenuContent(commentId: string) {
    const comment = this.comments?.find(comment => comment.id === commentId);
    const menu : MenuItem[] = [];
    let expandGitHubIssueMenu = true;
    if (comment && this.userProfile?.userName === comment.createdBy) {
      menu.push({
          label: 'Edit',
          icon: 'pi pi-pencil',
          command: () => {
          }
        });

      menu.push({
          label: 'Delete',
          icon: 'pi pi-trash',
          command: () => {
  
          }
        });

      menu.push({
        separator: true
      });
      expandGitHubIssueMenu = false;
    }
    menu.push({
        label: 'Create Github Issue',
        expanded: expandGitHubIssueMenu,
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

    return menu;
  }

  toggleActionMenu(event: any, commentId: string) {
    const menu: Menu | undefined = this.menus.find(menu => menu.el.nativeElement.getAttribute('data-comment-id') === commentId);
    if (menu) {
      menu.toggle(event);
    }
  }
}
