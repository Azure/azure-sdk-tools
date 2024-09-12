import { NgModule } from '@angular/core';
import { ReviewInfoComponent } from 'src/app/_components/shared/review-info/review-info.component';
import { ConversationsComponent } from 'src/app/_components/conversations/conversations.component';
import { CommentThreadComponent } from 'src/app/_components/shared/comment-thread/comment-thread.component';
import { ReviewPageLayoutComponent } from 'src/app/_components/shared/review-page-layout/review-page-layout.component';
import { MarkdownToHtmlPipe } from 'src/app/_pipes/markdown-to-html.pipe';
import { EditorComponent } from 'src/app/_components/shared/editor/editor.component';
import { EditorModule } from 'primeng/editor';
import { PanelModule } from 'primeng/panel';
import { MenuModule } from 'primeng/menu';
import { TimelineModule } from 'primeng/timeline';
import { DividerModule } from 'primeng/divider';
import { RevisionOptionsComponent } from 'src/app/_components/revision-options/revision-options.component';
import { SharedAppModule } from './shared-app.module';
import { CommonModule } from '@angular/common'; 
import { PageOptionsSectionComponent } from 'src/app/_components/shared/page-options-section/page-options-section.component';
import { NavBarComponent } from 'src/app/_components/shared/nav-bar/nav-bar.component';

@NgModule({
  declarations: [
    ReviewInfoComponent,
    CommentThreadComponent,
    ConversationsComponent,
    ReviewPageLayoutComponent,
    RevisionOptionsComponent,
    PageOptionsSectionComponent,
    MarkdownToHtmlPipe,
    EditorComponent,
  ],
  exports: [
    ReviewInfoComponent,
    CommentThreadComponent,
    ConversationsComponent,
    PageOptionsSectionComponent,
    ReviewPageLayoutComponent,
    RevisionOptionsComponent,
    MarkdownToHtmlPipe,
    EditorComponent,
    SharedAppModule,
    EditorModule,
    PanelModule,
    MenuModule,
    TimelineModule,
    DividerModule
  ],
  imports: [
    CommonModule,
    SharedAppModule,
    EditorModule,
    PanelModule,
    MenuModule,
    TimelineModule,
    DividerModule
  ]
})
export class ReviewPageLayoutModule { }
