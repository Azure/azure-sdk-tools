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
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { InputGroupModule } from 'primeng/inputgroup';
import { InputGroupAddonModule } from 'primeng/inputgroupaddon';
import { SplitterModule } from 'primeng/splitter';
import { SidebarModule } from 'primeng/sidebar';
import { TimeagoModule } from 'ngx-timeago';
import { SelectButtonModule } from 'primeng/selectbutton';
import { FileUploadModule } from 'primeng/fileupload';
import { InputTextModule } from 'primeng/inputtext';
import { MessagesModule } from 'primeng/messages';
import { BadgeModule } from 'primeng/badge';
import { SimplemdeModule } from 'ngx-simplemde';
import { MonacoEditorModule, NgxMonacoEditorConfig  } from 'ngx-monaco-editor-v2';
import { environment } from 'src/environments/environment';

const monacoEditorConfig: NgxMonacoEditorConfig = {
  baseUrl: environment.assetsPath 
};
 

@NgModule({
  declarations: [
    NavBarComponent,
    FooterComponent,
    RevisionsListComponent,
    LanguageNamesPipe,
    LastUpdatedOnPipe,
    ApprovalPipe
  ],
  exports: [
    NavBarComponent,
    FooterComponent,
    RevisionsListComponent,
    LanguageNamesPipe,
    LastUpdatedOnPipe,
    ApprovalPipe,
    BadgeModule,
    ContextMenuModule,
    TableModule,
    ChipModule,
    DropdownModule,
    MenubarModule,
    MessagesModule,
    MultiSelectModule,
    FormsModule,
    IconFieldModule,
    InputIconModule,
    FileUploadModule,
    ReactiveFormsModule,
    SelectButtonModule,
    SplitterModule,
    SidebarModule,
    TimeagoModule,
    InputTextModule,
    SimplemdeModule,
    MonacoEditorModule
  ],
  imports: [
    CommonModule,
    BadgeModule,
    ContextMenuModule,
    TableModule,
    ChipModule,
    DropdownModule,
    MenubarModule,
    MessagesModule,
    MultiSelectModule,
    FormsModule,
    IconFieldModule,
    InputIconModule,
    FileUploadModule,
    ReactiveFormsModule,
    SelectButtonModule,
    SplitterModule,
    SidebarModule,
    InputTextModule,
    TimeagoModule.forRoot(),
    SimplemdeModule.forRoot(),
    MonacoEditorModule.forRoot(monacoEditorConfig)
  ]
})
export class SharedAppModule { }
