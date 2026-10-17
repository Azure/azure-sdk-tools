import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { ReviewPageLayoutModule } from './shared/review-page-layout.module';
import { CommonModule } from '@angular/common';
import { SamplesPageComponent } from '../_components/samples-page/samples-page.component';
import { DialogModule } from 'primeng/dialog';
import { TableModule } from 'primeng/table';

const routes: Routes = [
  { path: '', component: SamplesPageComponent }
];

@NgModule({
  imports: [
    CommonModule,
    DialogModule,
    TableModule,
    ReviewPageLayoutModule,
    SamplesPageComponent,
    RouterModule.forChild(routes),
  ]
})
export class SamplesPageModule { }
