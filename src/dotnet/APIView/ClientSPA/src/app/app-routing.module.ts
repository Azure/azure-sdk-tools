import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';

import { IndexPageComponent } from './_components/index-page/index-page.component';
import { AuthGuard } from './_guards/auth.guard';
import { FeaturesGuard } from './_guards/features.guard';
import { ReviewPageComponent } from './_components/review-page/review-page.component';

const routes: Routes = [
  { path: '', component: IndexPageComponent, canActivate: [AuthGuard, FeaturesGuard] },
  { path: '',
     runGuardsAndResolvers: 'always',
     canActivate: [AuthGuard],
     children: [
      { path: 'review/:reviewId', loadChildren: () => import('./_modules/review-page.module').then(m => m.ReviewPageModule) }, // Lazy load review page module
      { path: 'conversation/:reviewId', loadChildren: () => import('./_modules/conversation-page.module').then(m => m.ConversationPageModule) },
      { path: 'revision/:reviewId', loadChildren: () => import('./_modules/revision-page.module').then(m => m.RevisionPageModule) }
     ]
  },
  { path: '**', component: IndexPageComponent, pathMatch: 'full' }
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
