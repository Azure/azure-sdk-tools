import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { CommonModule } from '@angular/common';
import { NamespacePageComponent } from '../_components/namespace-page/namespace-page.component';
import { ReviewPageLayoutModule } from './shared/review-page-layout.module';

const routes: Routes = [
  { path: '', component: NamespacePageComponent }
];

@NgModule({
  imports: [
    CommonModule,
    ReviewPageLayoutModule,
    NamespacePageComponent,
    RouterModule.forChild(routes),
  ]
})
export class NamespacePageModule { }
