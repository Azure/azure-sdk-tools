import { Component, Injector, Input } from '@angular/core';
import { UserProfile } from 'src/app/_models/auth_service_models';
import { CommentItemModel } from 'src/app/_models/review';
import { AuthService } from 'src/app/_services/auth/auth.service';
import { ConfigService } from 'src/app/_services/config/config.service';

@Component({
  selector: 'app-comment-thread',
  templateUrl: './comment-thread.component.html',
  styleUrls: ['./comment-thread.component.scss'],
  host: {
    'class': 'comment-thread'
  }
})
export class CommentThreadComponent {
  @Input() comments: CommentItemModel[] | undefined = []
  userProfile : UserProfile | undefined;
  commentEditText: string | undefined;

  constructor(private authService: AuthService, private configService: ConfigService) { }

  ngOnInit(): void {
    this.authService.getUserProfile().subscribe(
      (userProfile : any) => {
        this.userProfile = userProfile;
      });
  }
}
