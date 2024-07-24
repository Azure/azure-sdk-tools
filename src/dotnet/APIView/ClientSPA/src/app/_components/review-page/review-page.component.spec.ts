import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ReviewPageComponent } from './review-page.component';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { NavBarComponent } from '../shared/nav-bar/nav-bar.component';
import { ReviewInfoComponent } from '../shared/review-info/review-info.component';
import { MenuModule } from 'primeng/menu';
import { SplitterModule } from 'primeng/splitter';
import { SidebarModule } from 'primeng/sidebar';
import { FooterComponent } from '../shared/footer/footer.component';
import { BreadcrumbModule } from 'primeng/breadcrumb';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { CodePanelComponent } from '../code-panel/code-panel.component';
import { ReviewsListComponent } from '../reviews-list/reviews-list.component';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { RevisionsListComponent } from '../revisions-list/revisions-list.component';
import { MenubarModule } from 'primeng/menubar';
import { ContextMenuModule } from 'primeng/contextmenu';
import { of } from 'rxjs';
import { ApprovalPipe } from 'src/app/_pipes/approval.pipe';
import { ReviewNavComponent } from '../review-nav/review-nav.component';
import { ReviewPageOptionsComponent } from '../review-page-options/review-page-options.component';
import { PageOptionsSectionComponent } from '../shared/page-options-section/page-options-section.component';
import { TimelineModule } from 'primeng/timeline';
import { DialogModule } from 'primeng/dialog';
import { PanelModule } from 'primeng/panel';
import { DropdownModule } from 'primeng/dropdown';
import { InputSwitchModule } from 'primeng/inputswitch';
import { MultiSelectModule } from 'primeng/multiselect';
import { SharedAppModule } from 'src/app/_modules/shared/shared-app.module';
import { ReviewPageModule } from 'src/app/_modules/review-page/review-page.module';

describe('ReviewPageComponent', () => {
  let component: ReviewPageComponent;
  let fixture: ComponentFixture<ReviewPageComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      declarations: [ 
        ReviewPageComponent,
        ReviewNavComponent,
        ReviewPageOptionsComponent,
        PageOptionsSectionComponent,
        NavBarComponent,
        ReviewInfoComponent,
        FooterComponent,
        CodePanelComponent,
        ReviewsListComponent,
        RevisionsListComponent,
        ApprovalPipe
      ],
      imports: [
        HttpClientTestingModule,
        BrowserAnimationsModule,
        SharedAppModule,
        ReviewPageModule
      ],
      providers: [
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ reviewId: 'test' }),
            },
            queryParams: of(convertToParamMap({ activeApiRevisionId: 'test', diffApiRevisionId: 'test' }))
          }
        }
      ]
    });
    fixture = TestBed.createComponent(ReviewPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});