import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';

import { IndexPageComponent } from './_components/index-page/index-page.component';
import { AuthGuard } from './_guards/auth.guard';
import { ADMIN_PERMISSIONS_PAGE_NAME, CONVERSATION_PAGE_NAME, INDEX_PAGE_NAME, PROFILE_PAGE_NAME, REVIEW_PAGE_NAME, REVISION_PAGE_NAME, SAMPLES_PAGE_NAME } from './_helpers/router-helpers';
import { ProfilePageComponent } from './_components/profile-page/profile-page.component';
import { AdminPermissionsPageComponent } from './_components/admin-permissions-page/admin-permissions-page.component';
import { ThemeTestComponent } from './_components/theme-test/theme-test.component';

const routes: Routes = [
  { path: 'theme-test', component: ThemeTestComponent }, // Dev-only: theme comparison page
  { path: '', component: IndexPageComponent, canActivate: [AuthGuard], data: { pageName: INDEX_PAGE_NAME } },
  { path: '',
     runGuardsAndResolvers: 'always',
     canActivate: [AuthGuard],
     children: [
      { path: 'review/:reviewId', loadChildren: () => import('./_modules/review-page.module').then(m => m.ReviewPageModule), data: { pageName: REVIEW_PAGE_NAME } }, // Lazy load review page module
      { path: 'conversation/:reviewId', loadChildren: () => import('./_modules/conversation-page.module').then(m => m.ConversationPageModule), data: { pageName: CONVERSATION_PAGE_NAME} },
      { path: 'revision/:reviewId', loadChildren: () => import('./_modules/revision-page.module').then(m => m.RevisionPageModule), data: { pageName: REVISION_PAGE_NAME }  },
      { path: 'samples/:reviewId', loadChildren: () => import('./_modules/samples-page.module').then(m => m.SamplesPageModule), data: { pageName: SAMPLES_PAGE_NAME } },
      { path: 'profile/:userName', component: ProfilePageComponent, data: { pageName: PROFILE_PAGE_NAME }},
      { path: 'admin/permissions', component: AdminPermissionsPageComponent, data: { pageName: ADMIN_PERMISSIONS_PAGE_NAME }}
     ]
  },
  { path: '**', component: IndexPageComponent, pathMatch: 'full' }
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
