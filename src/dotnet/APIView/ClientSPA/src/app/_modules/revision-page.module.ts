import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReviewPageLayoutModule } from './shared/review-page-layout.module';
import { RouterModule, Routes } from '@angular/router';
import { RevisionPageComponent } from '../_components/revision-page/revision-page.component';

const routes: Routes = [
  { path: '', component: RevisionPageComponent }
];

@NgModule({
  declarations: [
  ],
  imports: [
    CommonModule,
    ReviewPageLayoutModule,
    RouterModule.forChild(routes),
    RevisionPageComponent
  ]
})
export class RevisionPageModule { }
