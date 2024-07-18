import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Routes } from '@angular/router';
import { ReviewPageComponent } from 'src/app/_components/review-page/review-page.component';
import { ReviewNavComponent } from 'src/app/_components/review-nav/review-nav.component';
import { ReviewInfoComponent } from 'src/app/_components/shared/review-info/review-info.component';
import { CodePanelComponent } from 'src/app/_components/code-panel/code-panel.component';
import { CommentThreadComponent } from 'src/app/_components/shared/comment-thread/comment-thread.component';
import { DialogModule } from 'primeng/dialog';
import { EditorModule } from 'primeng/editor';
import { PanelModule } from 'primeng/panel';
import { TreeSelectModule } from 'primeng/treeselect';
import { MenuModule } from 'primeng/menu';
import { TimelineModule } from 'primeng/timeline';
import { SharedAppModule } from '../shared/shared-app.module';
import { ButtonModule } from 'primeng/button';
import { UiScrollModule  } from 'ngx-ui-scroll' ;
import { PageOptionsSectionComponent } from 'src/app/_components/shared/page-options-section/page-options-section.component';
import { ApiRevisionOptionsComponent } from 'src/app/_components/api-revision-options/api-revision-options.component';
import { MarkdownToHtmlPipe } from 'src/app/_pipes/markdown-to-html.pipe';
import { EditorComponent } from 'src/app/_components/shared/editor/editor.component';
import { ReviewPageOptionsComponent } from 'src/app/_components/review-page-options/review-page-options.component';
import { InputSwitchModule } from 'primeng/inputswitch';
import { ConversationComponent } from 'src/app/_components/conversation/conversation.component';

const routes: Routes = [
  { path: '', component: ReviewPageComponent }
];

@NgModule({
  declarations: [
    ReviewPageComponent,
    ReviewNavComponent,
    ReviewInfoComponent,
    CodePanelComponent,
    CommentThreadComponent,
    ConversationComponent,
    PageOptionsSectionComponent,
    ReviewPageOptionsComponent,
    ApiRevisionOptionsComponent,
    MarkdownToHtmlPipe,
    EditorComponent
  ],
  imports: [
    SharedAppModule,
    CommonModule,
    EditorModule,
    PanelModule,
    DialogModule,
    TreeSelectModule,
    MenuModule,
    TimelineModule,
    ButtonModule,
    InputSwitchModule,
    UiScrollModule,
    RouterModule.forChild(routes),
  ]
})
export class ReviewPageModule { }
