import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NavBarComponent } from 'src/app/_components/shared/nav-bar/nav-bar.component';
import { FooterComponent } from 'src/app/_components/shared/footer/footer.component';
import { LanguageNamesPipe } from 'src/app/_pipes/language-names.pipe';
import { LastUpdatedOnPipe } from 'src/app/_pipes/last-updated-on.pipe';
import { ApprovalPipe } from 'src/app/_pipes/approval.pipe';
import { RevisionsListComponent } from 'src/app/_components/revisions-list/revisions-list.component';
import { ContextMenuModule } from 'primeng/contextmenu';
import { TableModule } from 'primeng/table';
import { ChipModule } from 'primeng/chip';
import { DropdownModule } from 'primeng/dropdown';
import { MenubarModule } from 'primeng/menubar';
import { MultiSelectModule } from 'primeng/multiselect';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { SplitterModule } from 'primeng/splitter';
import { SidebarModule } from 'primeng/sidebar';
import { TimeagoModule } from 'ngx-timeago';
import { SelectButtonModule } from 'primeng/selectbutton';
import { FileUploadModule } from 'primeng/fileupload';
import { InputTextModule } from 'primeng/inputtext';
import { MessagesModule } from 'primeng/messages';
import { BadgeModule } from 'primeng/badge';
import { ToastModule } from 'primeng/toast';
import { MenuModule } from 'primeng/menu';
import { ReviewInfoComponent } from 'src/app/_components/shared/review-info/review-info.component';
import { ApiRevisionOptionsComponent } from 'src/app/_components/api-revision-options/api-revision-options.component';
import { ReviewPageLayoutComponent } from 'src/app/_components/shared/review-page-layout/review-page-layout.component';
 

@NgModule({
  declarations: [
    NavBarComponent,
    FooterComponent,
    RevisionsListComponent,
    ReviewInfoComponent,
    ApiRevisionOptionsComponent,
    ReviewPageLayoutComponent,
    LanguageNamesPipe,
    LastUpdatedOnPipe,
    ApprovalPipe
  ],
  exports: [
    NavBarComponent,
    FooterComponent,
    RevisionsListComponent,
    ReviewInfoComponent,
    ApiRevisionOptionsComponent,
    ReviewPageLayoutComponent,
    LanguageNamesPipe,
    LastUpdatedOnPipe,
    ApprovalPipe,
    BadgeModule,
    ContextMenuModule,
    TableModule,
    ChipModule,
    DropdownModule,
    MenubarModule,
    MenuModule,
    MessagesModule,
    MultiSelectModule,
    FormsModule,
    FileUploadModule,
    ReactiveFormsModule,
    SelectButtonModule,
    SplitterModule,
    SidebarModule,
    TimeagoModule,
    InputTextModule,
  ],
  imports: [
    BadgeModule,
    CommonModule,
    ContextMenuModule,
    TableModule,
    ChipModule,
    DropdownModule,
    MenubarModule,
    MenuModule,
    MessagesModule,
    MultiSelectModule,
    FormsModule,
    FileUploadModule,
    ReactiveFormsModule,
    SelectButtonModule,
    SplitterModule,
    SidebarModule,
    InputTextModule,
    TimeagoModule.forRoot(),
  ]
})
export class SharedAppModule { }
