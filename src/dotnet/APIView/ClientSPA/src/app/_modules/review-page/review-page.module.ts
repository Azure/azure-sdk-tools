import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Routes } from '@angular/router';
import { ReviewPageComponent } from 'src/app/_components/review-page/review-page.component';
import { ReviewNavComponent } from 'src/app/_components/review-nav/review-nav.component';
import { CodePanelComponent } from 'src/app/_components/code-panel/code-panel.component';
import { CommentThreadComponent } from 'src/app/_components/shared/comment-thread/comment-thread.component';
import { DialogModule } from 'primeng/dialog';
import { EditorModule } from 'primeng/editor';
import { PanelModule } from 'primeng/panel';
import { TreeSelectModule } from 'primeng/treeselect';
import { TimelineModule } from 'primeng/timeline';
import { SharedAppModule } from '../shared/shared-app.module';
import { ButtonModule } from 'primeng/button';
import { DividerModule } from 'primeng/divider';
import { UiScrollModule  } from 'ngx-ui-scroll' ;
import { PageOptionsSectionComponent } from 'src/app/_components/shared/page-options-section/page-options-section.component';
import { MarkdownToHtmlPipe } from 'src/app/_pipes/markdown-to-html.pipe';
import { EditorComponent } from 'src/app/_components/shared/editor/editor.component';
import { ReviewPageOptionsComponent } from 'src/app/_components/review-page-options/review-page-options.component';
import { InputSwitchModule } from 'primeng/inputswitch';
import { ConversationsComponent } from 'src/app/_components/conversations/conversations.component';

const routes: Routes = [
  { path: '', component: ReviewPageComponent }
];

@NgModule({
  declarations: [
    ReviewPageComponent,
    ReviewNavComponent,
    CodePanelComponent,
    CommentThreadComponent,
    ConversationsComponent,
    PageOptionsSectionComponent,
    ReviewPageOptionsComponent,
    MarkdownToHtmlPipe,
    EditorComponent,
  ],
  imports: [
    SharedAppModule,
    CommonModule,
    EditorModule,
    PanelModule,
    DialogModule,
    TreeSelectModule,
    TimelineModule,
    ButtonModule,
    InputSwitchModule,
    UiScrollModule,
    DividerModule,
    RouterModule.forChild(routes),
  ]
})
export class ReviewPageModule { }
