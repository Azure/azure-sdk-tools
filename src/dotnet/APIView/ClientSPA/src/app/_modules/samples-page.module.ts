import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { ReviewPageLayoutModule } from './shared/review-page-layout.module';
import { CommonModule } from '@angular/common';
import { SamplesPageComponent } from '../_components/samples-page/samples-page.component';
import { CodeEditorComponent } from '../_components/shared/code-editor/code-editor.component';
import { DialogModule } from 'primeng/dialog';

const routes: Routes = [
  { path: '', component: SamplesPageComponent }
];

@NgModule({
  declarations: [
    SamplesPageComponent,
    CodeEditorComponent
  ],
  imports: [
    CommonModule,
    DialogModule,
    ReviewPageLayoutModule,
    RouterModule.forChild(routes),
  ]
})
export class SamplesPageModule { }
