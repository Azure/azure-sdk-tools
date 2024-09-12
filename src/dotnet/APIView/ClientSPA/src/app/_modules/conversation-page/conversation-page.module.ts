import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SharedAppModule } from '../shared/shared-app.module';
import { RouterModule, Routes } from '@angular/router';
import { ConversationPageComponent } from 'src/app/_components/conversation-page/conversation-page.component';

const routes: Routes = [
  { path: '', component: ConversationPageComponent }
];

@NgModule({
  declarations: [
    ConversationPageComponent,
  ],
  imports: [
    SharedAppModule,
    CommonModule,
    RouterModule.forChild(routes),
  ]
})
export class ConversationPageModule { }
