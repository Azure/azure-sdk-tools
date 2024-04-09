import { NgModule, APP_INITIALIZER } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { HttpClientModule } from '@angular/common/http';
import { ReactiveFormsModule } from '@angular/forms';

import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';
import { NavBarComponent } from './_components/shared/nav-bar/nav-bar.component';
import { IndexPageComponent } from './_components/index-page/index-page.component';
import { ReviewsListComponent } from './_components/reviews-list/reviews-list.component';
import { FooterComponent } from './_components/shared/footer/footer.component';
import { MenubarModule } from 'primeng/menubar';
import { MenuModule } from 'primeng/menu';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { InputTextModule } from 'primeng/inputtext';
import { PaginatorModule } from 'primeng/paginator';
import { TabMenuModule } from 'primeng/tabmenu';
import { ToolbarModule } from 'primeng/toolbar';
import { DropdownModule } from 'primeng/dropdown';
import { MultiSelectModule } from 'primeng/multiselect';
import { TreeSelectModule } from 'primeng/treeselect';
import { SidebarModule } from 'primeng/sidebar';
import { TimeagoModule } from "ngx-timeago";
import { ChipModule } from 'primeng/chip';
import { BadgeModule } from 'primeng/badge';
import { BreadcrumbModule } from 'primeng/breadcrumb';
import { ImageModule } from 'primeng/image';
import { LanguageNamesPipe } from './_pipes/language-names.pipe';
import { AvatarModule } from 'primeng/avatar';
import { ContextMenuModule } from 'primeng/contextmenu';
import { EditorModule } from 'primeng/editor';
import { FileUploadModule } from 'primeng/fileupload';
import { TooltipModule } from 'primeng/tooltip';
import { SplitterModule } from 'primeng/splitter';
import { SplitButtonModule } from 'primeng/splitbutton';
import { TimelineModule } from 'primeng/timeline';
import { VirtualScrollerModule } from 'primeng/virtualscroller';
import { RevisionsListComponent } from './_components/revisions-list/revisions-list.component';
import { ApprovalPipe } from './_pipes/approval.pipe';
import { LastUpdatedOnPipe } from './_pipes/last-updated-on.pipe';
import { Observable } from 'rxjs';
import { ConfigService } from './_services/config/config.service';
import { CookieService } from 'ngx-cookie-service';
import { ReviewPageComponent } from './_components/review-page/review-page.component';
import { ReviewNavComponent } from './_components/review-nav/review-nav.component';
import { ReviewInfoComponent } from './_components/shared/review-info/review-info.component';
import { CodePanelComponent } from './_components/code-panel/code-panel.component';
import { CommentThreadComponent } from './_components/shared/comment-thread/comment-thread.component';

export function initializeApp(configService: ConfigService) {
  return (): Observable<any> => {
    return configService.loadConfig();
  }
}

@NgModule({
  declarations: [
    AppComponent,
    IndexPageComponent,
    LanguageNamesPipe,
    NavBarComponent,
    ReviewsListComponent,
    FooterComponent,
    RevisionsListComponent,
    ApprovalPipe,
    LastUpdatedOnPipe,
    ReviewPageComponent,
    ReviewNavComponent,
    ReviewInfoComponent,
    CodePanelComponent,
    CommentThreadComponent
  ],
  imports: [
    AppRoutingModule,
    AvatarModule,
    BadgeModule,
    BreadcrumbModule,
    BrowserModule,
    BrowserAnimationsModule,
    ButtonModule,
    ChipModule,
    ContextMenuModule,
    TabMenuModule,
    ToolbarModule,
    DropdownModule,
    FileUploadModule,
    HttpClientModule,
    ImageModule,
    InputTextModule,
    MenubarModule,
    MenuModule,
    MultiSelectModule,
    PaginatorModule,
    ReactiveFormsModule,
    SidebarModule,
    SplitterModule,
    SplitButtonModule,
    TableModule,
    TimeagoModule.forRoot(),
    TimelineModule,
    TooltipModule,
    TreeSelectModule,
    VirtualScrollerModule,
    EditorModule,

  ],
  providers: [
    ConfigService,
    {
      provide: APP_INITIALIZER,
      useFactory: initializeApp,
      deps: [ConfigService],
      multi: true
    },
    CookieService
  ],
  bootstrap: [AppComponent]
})
export class AppModule { }
