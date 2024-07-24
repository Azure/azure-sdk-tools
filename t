[1mdiff --git a/src/dotnet/APIView/ClientSPA/src/app/_components/review-page/review-page.component.spec.ts b/src/dotnet/APIView/ClientSPA/src/app/_components/review-page/review-page.component.spec.ts[m
[1mindex b492b0691..6a36885f7 100644[m
[1m--- a/src/dotnet/APIView/ClientSPA/src/app/_components/review-page/review-page.component.spec.ts[m
[1m+++ b/src/dotnet/APIView/ClientSPA/src/app/_components/review-page/review-page.component.spec.ts[m
[36m@@ -26,6 +26,7 @@[m [mimport { TimelineModule } from 'primeng/timeline';[m
 import { DialogModule } from 'primeng/dialog';[m
 import { PanelModule } from 'primeng/panel';[m
 import { DropdownModule } from 'primeng/dropdown';[m
[32m+[m[32mimport { FileUploadModule } from 'primeng/fileupload';[m
 [m
 describe('ReviewPageComponent', () => {[m
   let component: ReviewPageComponent;[m
[36m@@ -42,7 +43,6 @@[m [mdescribe('ReviewPageComponent', () => {[m
         ReviewInfoComponent,[m
         FooterComponent,[m
         CodePanelComponent,[m
[31m-        ReviewsListComponent,[m
         RevisionsListComponent,[m
         ApprovalPipe[m
       ],[m
[36m@@ -59,8 +59,8 @@[m [mdescribe('ReviewPageComponent', () => {[m
         DropdownModule,[m
         DialogModule,[m
         PanelModule,[m
[31m-        ReactiveFormsModule,[m
[31m-        FormsModule[m
[32m+[m[32m        FormsModule,[m
[32m+[m[32m        ReactiveFormsModule[m
       ],[m
       providers: [[m
         {[m
[1mdiff --git a/src/dotnet/APIView/ClientSPA/src/app/_components/revisions-list/revisions-list.component.spec.ts b/src/dotnet/APIView/ClientSPA/src/app/_components/revisions-list/revisions-list.component.spec.ts[m
[1mindex 9d8bc4e5a..af01cd585 100644[m
[1m--- a/src/dotnet/APIView/ClientSPA/src/app/_components/revisions-list/revisions-list.component.spec.ts[m
[1m+++ b/src/dotnet/APIView/ClientSPA/src/app/_components/revisions-list/revisions-list.component.spec.ts[m
[36m@@ -5,9 +5,9 @@[m [mimport { RevisionsListComponent } from './revisions-list.component';[m
 import { ContextMenuModule } from 'primeng/contextmenu';[m
 import { SidebarModule } from 'primeng/sidebar';[m
 import { TabMenuModule } from 'primeng/tabmenu';[m
[31m-import { SimpleChanges } from '@angular/core';[m
[31m-import { Dropdown, DropdownModule } from 'primeng/dropdown';[m
[32m+[m[32mimport { DropdownModule } from 'primeng/dropdown';[m
 import { FormsModule, ReactiveFormsModule } from '@angular/forms';[m
[32m+[m[32mimport { FileUploadModule } from 'primeng/fileupload';[m
 [m
 describe('RevisionListComponent', () => {[m
   let component: RevisionsListComponent;[m
[36m@@ -22,6 +22,7 @@[m [mdescribe('RevisionListComponent', () => {[m
         SidebarModule,[m
         TabMenuModule,[m
         DropdownModule,[m
[32m+[m[32m        FileUploadModule,[m
         ReactiveFormsModule,[m
         FormsModule[m
       ][m
[1mdiff --git a/src/dotnet/APIView/ClientSPA/src/app/_components/shared/review-info/review-info.component.html b/src/dotnet/APIView/ClientSPA/src/app/_components/shared/review-info/review-info.component.html[m
[1mindex e20aed5fe..d2787150a 100644[m
[1m--- a/src/dotnet/APIView/ClientSPA/src/app/_components/shared/review-info/review-info.component.html[m
[1m+++ b/src/dotnet/APIView/ClientSPA/src/app/_components/shared/review-info/review-info.component.html[m
[36m@@ -10,7 +10,7 @@[m
             </app-api-revision-options>[m
         </ng-template>[m
         <ng-template pTemplate="end">[m
[31m-            <input type="checkbox" (change)="onRightPanelCheckChange($event)" [(ngModel)]="showPageOptions" class="btn-check" id="page-right-panel" autocomplete="off">[m
[32m+[m[32m            <input type="checkbox" (change)="onRightPanelCheckChange($event)" [(ngModel)]="showPageOptions" class="btn-check" id="page-right-panel" name="page-right-panel" autocomplete="off">[m
             <label class="btn btn-sm btn-outline-primary float-end" accesskey="m" for="page-right-panel"><i class="fa fa-bars"></i></label>[m
         </ng-template>[m
     </p-menubar>[m
