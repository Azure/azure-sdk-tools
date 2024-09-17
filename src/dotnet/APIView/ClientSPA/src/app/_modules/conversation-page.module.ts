import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { ConversationPageComponent } from 'src/app/_components/conversation-page/conversation-page.component';
import { ReviewPageLayoutModule } from './shared/review-page-layout.module';
import { CommonModule } from '@angular/common';

const routes: Routes = [
  { path: '', component: ConversationPageComponent }
];

@NgModule({
  declarations: [
    ConversationPageComponent
  ],
  imports: [
    CommonModule,
    ReviewPageLayoutModule,
    RouterModule.forChild(routes),
  ]
})
export class ConversationPageModule { }
