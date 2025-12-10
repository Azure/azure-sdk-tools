import { ComponentFixture, TestBed } from '@angular/core/testing';

import { SamplesPageComponent } from './samples-page.component';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { of } from 'rxjs';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ReviewPageLayoutComponent } from '../shared/review-page-layout/review-page-layout.component';
import { ReviewInfoComponent } from '../shared/review-info/review-info.component';
import { NavBarComponent } from '../shared/nav-bar/nav-bar.component';
import { LanguageNamesPipe } from 'src/app/_pipes/language-names.pipe';
import { MenuModule } from 'primeng/menu';
import { MenubarModule } from 'primeng/menubar';
import { SplitterModule } from 'primeng/splitter';
import { SidebarModule } from 'primeng/sidebar';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { PageOptionsSectionComponent } from '../shared/page-options-section/page-options-section.component';
import { PanelModule } from 'primeng/panel';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';
import { Dialog, DialogModule } from 'primeng/dialog';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';

describe('SamplesPageComponent', () => {
  let component: SamplesPageComponent;
  let fixture: ComponentFixture<SamplesPageComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
    declarations: [
        SamplesPageComponent,
        NavBarComponent,
        ReviewInfoComponent,
        ReviewPageLayoutComponent,
        PageOptionsSectionComponent,
        LanguageNamesPipe
    ],
    imports: [BrowserAnimationsModule,
        SplitterModule,
        SidebarModule,
        PanelModule,
        MenuModule,
        MenubarModule,
        ReactiveFormsModule,
        FormsModule,
        DialogModule],
    providers: [
        {
            provide: ActivatedRoute,
            useValue: {
                snapshot: {
                    paramMap: convertToParamMap({ reviewId: 'test' }),
                },
                queryParams: of(convertToParamMap({ activeSamplesRevisionId: 'test' }))
            },
        },
        MessageService,
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting()
    ]
});
    fixture = TestBed.createComponent(SamplesPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
