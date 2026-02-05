import { NgModule, inject, provideAppInitializer } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { HTTP_INTERCEPTORS, HttpClientModule } from '@angular/common/http';
import { FormsModule } from '@angular/forms';

import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';
import { IndexPageComponent } from './_components/index-page/index-page.component';
import { ReviewsListComponent } from './_components/reviews-list/reviews-list.component';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { BadgeModule } from 'primeng/badge';
import { DialogModule } from 'primeng/dialog';
import { SelectButtonModule } from 'primeng/selectbutton';
import { SelectModule } from 'primeng/select';
import { InputTextModule } from 'primeng/inputtext';
import { ButtonModule } from 'primeng/button';
import { TooltipModule } from 'primeng/tooltip';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { AutoCompleteModule } from 'primeng/autocomplete';
import { Observable } from 'rxjs';
import { ConfigService } from './_services/config/config.service';
import { CookieService } from 'ngx-cookie-service';
import { SharedAppModule } from './_modules/shared/shared-app.module';
import { HttpErrorInterceptorService } from './_services/http-error-interceptor/http-error-interceptor.service';
import { APP_BASE_HREF, CommonModule } from '@angular/common';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ProfilePageComponent } from './_components/profile-page/profile-page.component';
import { AdminPermissionsPageComponent } from './_components/admin-permissions-page/admin-permissions-page.component';
import { providePrimeNG } from 'primeng/config';
import Lara from '@primeuix/themes/lara';

export function initializeApp(configService: ConfigService) {
  return (): Observable<any> => {
    return configService.loadConfig();
  }
}

@NgModule({
  declarations: [
    AppComponent,
    IndexPageComponent,
    ReviewsListComponent,
    ProfilePageComponent,
    AdminPermissionsPageComponent
  ],
  imports: [
    SharedAppModule,
    CommonModule,
    AppRoutingModule,
    BadgeModule,
    BrowserModule,
    NoopAnimationsModule,  // Disabled animations to prevent continuous change detection
    ToolbarModule,
    ToastModule,
    HttpClientModule,
    FormsModule,
    DialogModule,
    SelectButtonModule,
    SelectModule,
    InputTextModule,
    ButtonModule,
    TooltipModule,
    ConfirmDialogModule,
    AutoCompleteModule
  ],
  providers: [
    ConfigService,
    provideAppInitializer(() => {
        const initializerFn = (initializeApp)(inject(ConfigService));
        return initializerFn();
      }),
    {
      provide: HTTP_INTERCEPTORS,
      useClass: HttpErrorInterceptorService,
      multi: true
    },
    { provide: APP_BASE_HREF, useValue: '/' },
    MessageService,
    ConfirmationService,
    CookieService,
    providePrimeNG({
      theme: {
        preset: Lara,
        options: {
          darkModeSelector: '.dark-theme, .dark-solarized-theme'
        }
      }
    })
  ],
  bootstrap: [AppComponent]
})
export class AppModule { }
