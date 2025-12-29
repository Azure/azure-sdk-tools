import { ComponentFixture, TestBed } from '@angular/core/testing';

import { RevisionPageComponent } from './revision-page.component';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { ReviewPageLayoutComponent } from '../shared/review-page-layout/review-page-layout.component';
import { NavBarComponent } from '../shared/nav-bar/nav-bar.component';
import { RevisionsListComponent } from '../revisions-list/revisions-list.component';
import { MessageService } from 'primeng/api';
import { ReviewInfoComponent } from '../shared/review-info/review-info.component';
import { MenubarModule } from 'primeng/menubar';
import { MenuModule } from 'primeng/menu';
import { ContextMenuModule } from 'primeng/contextmenu';
import { DrawerModule } from 'primeng/drawer';
import { SelectModule } from 'primeng/select';
import { TooltipModule } from 'primeng/tooltip';
import { RippleModule } from 'primeng/ripple';
import { LanguageNamesPipe } from 'src/app/_pipes/language-names.pipe';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';

describe('RevisionPageComponent', () => {
  let component: RevisionPageComponent;
  let fixture: ComponentFixture<RevisionPageComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [
        RevisionPageComponent,
        NavBarComponent,
        ReviewInfoComponent,
        RevisionsListComponent,
        ReviewPageLayoutComponent,
        LanguageNamesPipe,
        BrowserAnimationsModule,
        HttpClientTestingModule,
        MenubarModule,
        MenuModule,
        ContextMenuModule,
        SelectModule,
        DrawerModule,
        TooltipModule,
        RippleModule,
        ReactiveFormsModule,
        FormsModule
      ],
      providers: [
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ reviewId: 'test' })
            }
          }
        },
        MessageService
      ]
    });
    fixture = TestBed.createComponent(RevisionPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
