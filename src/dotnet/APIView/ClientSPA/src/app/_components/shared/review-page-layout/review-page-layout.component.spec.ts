import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ReviewPageLayoutComponent } from './review-page-layout.component';
import { NavBarComponent } from '../nav-bar/nav-bar.component';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { ReviewInfoComponent } from '../review-info/review-info.component';
import { MenubarModule } from 'primeng/menubar';
import { MenuModule } from 'primeng/menu';
import { FooterComponent } from '../footer/footer.component';
import { LanguageNamesPipe } from 'src/app/_pipes/language-names.pipe';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { ReviewNavComponent } from '../../review-nav/review-nav.component';

describe('ReviewPageLayoutComponent', () => {
  let component: ReviewPageLayoutComponent;
  let fixture: ComponentFixture<ReviewPageLayoutComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      declarations: [
        ReviewPageLayoutComponent,
        ReviewInfoComponent,
        NavBarComponent,
        FooterComponent,
        LanguageNamesPipe
      ],
      imports: [
        BrowserAnimationsModule,
        HttpClientTestingModule,
        MenubarModule,
        MenuModule
      ],
      providers: [
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ reviewId: 'test' }),
              queryParamMap: convertToParamMap({ activeApiRevisionId: 'test' })
            }
          }
        }
      ]
    });
    fixture = TestBed.createComponent(ReviewPageLayoutComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
