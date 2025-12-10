import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NavBarComponent } from 'src/app/_components/shared/nav-bar/nav-bar.component';
import { LanguageNamesPipe } from 'src/app/_pipes/language-names.pipe';
import { LastUpdatedOnPipe } from 'src/app/_pipes/last-updated-on.pipe';
import { ApprovalPipe } from 'src/app/_pipes/approval.pipe';
import { RevisionsListComponent } from 'src/app/_components/revisions-list/revisions-list.component';
import { ContextMenuModule } from 'primeng/contextmenu';
import { TableModule } from 'primeng/table';
import { ChipModule } from 'primeng/chip';
import { Select } from 'primeng/select';
import { MenubarModule } from 'primeng/menubar';
import { MultiSelectModule } from 'primeng/multiselect';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { SplitterModule } from 'primeng/splitter';
import { Drawer } from 'primeng/drawer';
import { TimeagoModule } from 'ngx-timeago';
import { SelectButtonModule } from 'primeng/selectbutton';
import { FileUploadModule } from 'primeng/fileupload';
import { InputTextModule } from 'primeng/inputtext';
import { Message } from 'primeng/message';
import { BadgeModule } from 'primeng/badge';
import { SimplemdeModule } from 'ngx-simplemde';
import { MonacoEditorModule, NgxMonacoEditorConfig  } from 'ngx-monaco-editor-v2';
import { environment } from 'src/environments/environment';
import { ToggleSwitch } from 'primeng/toggleswitch';

const monacoEditorConfig: NgxMonacoEditorConfig = {
  baseUrl: environment.assetsPath 
};
 

@NgModule({
  declarations: [
    NavBarComponent,
    RevisionsListComponent,
    LanguageNamesPipe,
    LastUpdatedOnPipe,
    ApprovalPipe
  ],
  exports: [
    NavBarComponent,
    RevisionsListComponent,
    LanguageNamesPipe,
    LastUpdatedOnPipe,
    ApprovalPipe,
    BadgeModule,
    ContextMenuModule,
    TableModule,
    ChipModule,
    Select,
    MenubarModule,
    Message,
    MultiSelectModule,
    FormsModule,
    IconFieldModule,
    InputIconModule,
    ToggleSwitch,
    FileUploadModule,
    ReactiveFormsModule,
    SelectButtonModule,
    SplitterModule,
    Drawer,
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
    Select,
    MenubarModule,
    Message,
    MultiSelectModule,
    FormsModule,
    IconFieldModule,
    InputIconModule,
    ToggleSwitch,
    FileUploadModule,
    ReactiveFormsModule,
    SelectButtonModule,
    SplitterModule,
    Drawer,
    InputTextModule,
    TimeagoModule.forRoot(),
    SimplemdeModule.forRoot(),
    MonacoEditorModule.forRoot(monacoEditorConfig)
  ]
})
export class SharedAppModule { }
