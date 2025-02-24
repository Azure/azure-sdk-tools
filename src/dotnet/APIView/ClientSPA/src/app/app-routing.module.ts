import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';

import { IndexPageComponent } from './_components/index-page/index-page.component';
import { AuthGuard } from './_guards/auth.guard';
import { FeaturesGuard } from './_guards/features.guard';
import { CONVERSATION_PAGE_NAME, INDEX_PAGE_NAME, REVIEW_PAGE_NAME, REVISION_PAGE_NAME, SAMPLES_PAGE_NAME } from './_helpers/router-helpers';

const routes: Routes = [
  { path: '', component: IndexPageComponent, canActivate: [AuthGuard, FeaturesGuard], data: { pageName: INDEX_PAGE_NAME } },
  { path: '',
     runGuardsAndResolvers: 'always',
     canActivate: [AuthGuard],
     children: [
      { path: 'review/:reviewId', loadChildren: () => import('./_modules/review-page.module').then(m => m.ReviewPageModule), data: { pageName: REVIEW_PAGE_NAME } }, // Lazy load review page module
      { path: 'conversation/:reviewId', loadChildren: () => import('./_modules/conversation-page.module').then(m => m.ConversationPageModule), data: { pageName: CONVERSATION_PAGE_NAME} },
      { path: 'revision/:reviewId', loadChildren: () => import('./_modules/revision-page.module').then(m => m.RevisionPageModule), data: { pageName: REVISION_PAGE_NAME }  },
      { path: 'samples/:reviewId', loadChildren: () => import('./_modules/samples-page.module').then(m => m.SamplesPageModule), data: { pageName: SAMPLES_PAGE_NAME } }
     ]
  },
  { path: '**', component: IndexPageComponent, pathMatch: 'full' }
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
