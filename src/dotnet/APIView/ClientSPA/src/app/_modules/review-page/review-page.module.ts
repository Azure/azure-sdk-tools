import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Routes } from '@angular/router';
import { ReviewPageComponent } from 'src/app/_components/review-page/review-page.component';
import { ReviewNavComponent } from 'src/app/_components/review-nav/review-nav.component';
import { ReviewInfoComponent } from 'src/app/_components/shared/review-info/review-info.component';
import { CodePanelComponent } from 'src/app/_components/code-panel/code-panel.component';
import { CommentThreadComponent } from 'src/app/_components/shared/comment-thread/comment-thread.component';
import { EditorModule } from 'primeng/editor';
import { PanelModule } from 'primeng/panel';
import { TreeSelectModule } from 'primeng/treeselect';
import { NavBarComponent } from 'src/app/_components/shared/nav-bar/nav-bar.component';
import { BreadcrumbModule } from 'primeng/breadcrumb';
import { MenuModule } from 'primeng/menu';
import { SplitterModule } from 'primeng/splitter';
import { SidebarModule } from 'primeng/sidebar';
import { TimelineModule } from 'primeng/timeline';
import { SharedAppModule } from '../shared/shared-app.module';
import { ButtonModule } from 'primeng/button';
import { TimeagoModule } from 'ngx-timeago';

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
  ],
  imports: [
    SharedAppModule,
    BreadcrumbModule,
    CommonModule,
    EditorModule,
    PanelModule,
    TreeSelectModule,
    MenuModule,
    SplitterModule,
    SidebarModule,
    TimelineModule,
    ButtonModule,
    RouterModule.forChild(routes),
    TimeagoModule.forRoot(),
  ]
})
export class ReviewPageModule { }
