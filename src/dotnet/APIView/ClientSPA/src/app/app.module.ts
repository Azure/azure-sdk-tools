import { NgModule } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { HttpClientModule } from '@angular/common/http';

import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';
import { NavBarComponent } from './_components/shared/nav-bar/nav-bar.component';
import { IndexPageComponent } from './_components/index-page/index-page.component';
import { ReviewsListComponent } from './_components/reviews-list/reviews-list.component';
import { FooterComponent } from './_components/shared/footer/footer.component';
import { MenubarModule } from 'primeng/menubar';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { InputTextModule } from 'primeng/inputtext';
import { PaginatorModule } from 'primeng/paginator';
import { TabMenuModule } from 'primeng/tabmenu';
import { DropdownModule } from 'primeng/dropdown';
import { MultiSelectModule } from 'primeng/multiselect';
import { TreeSelectModule } from 'primeng/treeselect';
import { SidebarModule } from 'primeng/sidebar';
import { TimeagoModule } from "ngx-timeago";
import { ChipModule } from 'primeng/chip';
import { BadgeModule } from 'primeng/badge';
import { ImageModule } from 'primeng/image';
import { LanguageNamesPipe } from './_pipes/language-names.pipe';
import { AvatarModule } from 'primeng/avatar';
import { ContextMenuModule } from 'primeng/contextmenu';
import { FileUploadModule } from 'primeng/fileupload';
import { TooltipModule } from 'primeng/tooltip';
import { ReviewPageComponent } from './_components/review-page/review-page.component';
import { SplitterModule } from 'primeng/splitter';
import { CodePanelComponent } from './_components/code-panel/code-panel.component';
import { VirtualScrollerModule } from 'primeng/virtualscroller';
import { ReviewInfoComponent } from './_components/review-info/review-info.component';
import { RevisionsListComponent } from './_components/revisions-list/revisions-list.component';
import { ReviewNavComponent } from './_components/review-nav/review-nav.component';
import { SanitizeHtmlPipe } from './_pipes/sanitize-html.pipe';

@NgModule({
  declarations: [
    AppComponent,
    IndexPageComponent,
    LanguageNamesPipe,
    NavBarComponent,
    ReviewsListComponent,
    FooterComponent,
    ReviewPageComponent,
    CodePanelComponent,
    ReviewInfoComponent,
    RevisionsListComponent,
    ReviewNavComponent,
    SanitizeHtmlPipe
  ],
  imports: [
    AppRoutingModule,
    AvatarModule,
    BadgeModule,
    BrowserModule,
    BrowserAnimationsModule,
    ButtonModule,
    ChipModule,
    ContextMenuModule,
    TabMenuModule,
    DropdownModule,
    FileUploadModule,
    HttpClientModule,
    ImageModule,
    InputTextModule,
    MenubarModule,
    MultiSelectModule,
    PaginatorModule,
    SidebarModule,
    SplitterModule,
    TableModule,
    TimeagoModule.forRoot(),
    TooltipModule,
    TreeSelectModule,
    VirtualScrollerModule
  ],
  providers: [],
  bootstrap: [AppComponent]
})
export class AppModule { }
