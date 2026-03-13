import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Routes } from '@angular/router';
import { ReviewPageComponent } from 'src/app/_components/review-page/review-page.component';
import { ReviewNavComponent } from 'src/app/_components/review-nav/review-nav.component';
import { CodePanelComponent } from 'src/app/_components/code-panel/code-panel.component';
import { DialogModule } from 'primeng/dialog';
import { TreeSelectModule } from 'primeng/treeselect';
import { TreeModule } from 'primeng/tree';
import { ButtonModule } from 'primeng/button';
import { UiScrollModule  } from 'ngx-ui-scroll' ;
import { ReviewPageOptionsComponent } from 'src/app/_components/review-page-options/review-page-options.component';
import { ReviewPageLayoutModule } from './shared/review-page-layout.module';
import { ReviewToolbarComponent } from 'src/app/_components/review-toolbar/review-toolbar.component';
import { ConversationsComponent } from 'src/app/_components/conversations/conversations.component';
import { RevisionsListComponent } from 'src/app/_components/revisions-list/revisions-list.component';
import { SamplesPageComponent } from 'src/app/_components/samples-page/samples-page.component';

const routes: Routes = [
  { path: '', component: ReviewPageComponent }
];

@NgModule({
  declarations: [
    ReviewPageComponent,
    ReviewNavComponent,
    CodePanelComponent,
    ReviewPageOptionsComponent,
  ],
  imports: [
    CommonModule,
    DialogModule,
    TreeSelectModule,
    TreeModule,
    ButtonModule,
    UiScrollModule,
    ReviewPageLayoutModule,
    ReviewToolbarComponent,
    ConversationsComponent,
    RevisionsListComponent,
    SamplesPageComponent,
    RouterModule.forChild(routes),
  ]
})
export class ReviewPageModule { }
