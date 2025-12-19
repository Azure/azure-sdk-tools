import { NgModule, inject, provideAppInitializer } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { HTTP_INTERCEPTORS, HttpClientModule } from '@angular/common/http';

import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';
import { IndexPageComponent } from './_components/index-page/index-page.component';
import { ReviewsListComponent } from './_components/reviews-list/reviews-list.component';
import { TabMenuModule } from 'primeng/tabmenu';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { BadgeModule } from 'primeng/badge';
import { Observable } from 'rxjs';
import { ConfigService } from './_services/config/config.service';
import { CookieService } from 'ngx-cookie-service';
import { SharedAppModule } from './_modules/shared/shared-app.module';
import { HttpErrorInterceptorService } from './_services/http-error-interceptor/http-error-interceptor.service';
import { MessageService } from 'primeng/api';
import { CommonModule } from '@angular/common';
import { ProfilePageComponent } from './_components/profile-page/profile-page.component';
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
    ProfilePageComponent
  ],
  imports: [
    SharedAppModule,
    CommonModule,
    AppRoutingModule,
    BadgeModule,
    BrowserModule,
    NoopAnimationsModule,  // Disabled animations to prevent continuous change detection
    TabMenuModule,
    ToolbarModule,
    ToastModule,
    HttpClientModule
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
    MessageService,
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
