import { NgModule, APP_INITIALIZER, isDevMode } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { HttpClientModule } from '@angular/common/http';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';

import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';
import { IndexPageComponent } from './_components/index-page/index-page.component';
import { ReviewsListComponent } from './_components/reviews-list/reviews-list.component';
import { MenubarModule } from 'primeng/menubar';
import { TableModule } from 'primeng/table';
import { InputTextModule } from 'primeng/inputtext';
import { TabMenuModule } from 'primeng/tabmenu';
import { ToolbarModule } from 'primeng/toolbar';
import { DropdownModule } from 'primeng/dropdown';
import { MultiSelectModule } from 'primeng/multiselect';
import { SidebarModule } from 'primeng/sidebar';
import { TimeagoModule } from "ngx-timeago";
import { ChipModule } from 'primeng/chip';
import { BadgeModule } from 'primeng/badge';
import { LanguageNamesPipe } from './_pipes/language-names.pipe';
import { ContextMenuModule } from 'primeng/contextmenu';
import { FileUploadModule } from 'primeng/fileupload';
import { SplitterModule } from 'primeng/splitter';
import { RevisionsListComponent } from './_components/revisions-list/revisions-list.component';
import { ApprovalPipe } from './_pipes/approval.pipe';
import { LastUpdatedOnPipe } from './_pipes/last-updated-on.pipe';
import { Observable } from 'rxjs';
import { ConfigService } from './_services/config/config.service';
import { CookieService } from 'ngx-cookie-service';
import { SharedAppModule } from './_modules/shared/shared-app.module';
import { ServiceWorkerModule } from '@angular/service-worker';
import { environment } from 'src/environments/environment';

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
    RevisionsListComponent,
    ApprovalPipe,
    LastUpdatedOnPipe
  ],
  imports: [
    SharedAppModule,
    AppRoutingModule,
    BadgeModule,
    BrowserModule,
    BrowserAnimationsModule,
    ChipModule,
    ContextMenuModule,
    TabMenuModule,
    ToolbarModule,
    DropdownModule,
    FileUploadModule,
    HttpClientModule,
    InputTextModule,
    MenubarModule,
    MultiSelectModule,
    FormsModule,
    ReactiveFormsModule,
    SidebarModule,
    SplitterModule,
    TableModule,
    TimeagoModule.forRoot(),
    ServiceWorkerModule.register('/spa/ngsw-worker.js', {
      enabled: environment.production,
      // Register the ServiceWorker as soon as the application is stable
      // or after 30 seconds (whichever comes first).
      registrationStrategy: 'registerWhenStable:30000'
    }),
  ],
  providers: [
    ConfigService,
    {
      provide: APP_INITIALIZER,
      useFactory: initializeApp,
      deps: [ConfigService],
      multi: true
    },
    CookieService
  ],
  bootstrap: [AppComponent]
})
export class AppModule { }
