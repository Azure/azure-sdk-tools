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
import { FooterComponent } from '../shared/footer/footer.component';
import { ContextMenuModule } from 'primeng/contextmenu';
import { SidebarModule } from 'primeng/sidebar';
import { DropdownModule } from 'primeng/dropdown';
import { LanguageNamesPipe } from 'src/app/_pipes/language-names.pipe';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';

describe('RevisionPageComponent', () => {
  let component: RevisionPageComponent;
  let fixture: ComponentFixture<RevisionPageComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      declarations: [
        RevisionPageComponent,
        NavBarComponent,
        FooterComponent,
        ReviewInfoComponent,
        RevisionsListComponent,
        ReviewPageLayoutComponent,
        LanguageNamesPipe
      ],
      imports: [
        BrowserAnimationsModule,
        HttpClientTestingModule,
        MenubarModule,
        MenuModule,
        ContextMenuModule,
        DropdownModule,
        SidebarModule,
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
