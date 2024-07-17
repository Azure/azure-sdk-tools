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
import { FormsModule } from '@angular/forms';
import { SplitterModule } from 'primeng/splitter';
import { SidebarModule } from 'primeng/sidebar';
import { TimeagoModule } from 'ngx-timeago';

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
    ContextMenuModule,
    TableModule,
    ChipModule,
    DropdownModule,
    MenubarModule,
    MultiSelectModule,
    FormsModule,
    SplitterModule,
    SidebarModule,
    TimeagoModule,
  ],
  imports: [
    CommonModule,
    ContextMenuModule,
    TableModule,
    ChipModule,
    DropdownModule,
    MenubarModule,
    MultiSelectModule,
    FormsModule,
    SplitterModule,
    SidebarModule,
    TimeagoModule.forRoot(),
  ]
})
export class SharedAppModule { }
