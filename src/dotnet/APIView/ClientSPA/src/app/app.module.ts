import { NgModule, APP_INITIALIZER } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { HTTP_INTERCEPTORS, provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import { providePrimeNG } from 'primeng/config';
import Lara from '@primeng/themes/lara';

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

export function initializeApp(configService: ConfigService) {
  return (): Observable<any> => {
    return configService.loadConfig();
  }
}

@NgModule({ declarations: [
        AppComponent,
        IndexPageComponent,
        ReviewsListComponent,
        ProfilePageComponent
    ],
    bootstrap: [AppComponent], imports: [SharedAppModule,
        CommonModule,
        AppRoutingModule,
        BadgeModule,
        BrowserModule,
        BrowserAnimationsModule,
        TabMenuModule,
        ToolbarModule,
        ToastModule], providers: [
        ConfigService,
        {
            provide: APP_INITIALIZER,
            useFactory: initializeApp,
            deps: [ConfigService],
            multi: true
        },
        {
            provide: HTTP_INTERCEPTORS,
            useClass: HttpErrorInterceptorService,
            multi: true
        },
        MessageService,
        CookieService,
        provideHttpClient(withInterceptorsFromDi()),
        providePrimeNG({
            theme: {
                preset: Lara
            }
        })
    ] })
export class AppModule { }
