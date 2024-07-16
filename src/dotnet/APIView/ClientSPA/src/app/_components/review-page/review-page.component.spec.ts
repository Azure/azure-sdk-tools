import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ReviewPageComponent } from './review-page.component';
import { ActivatedRoute, convertToParamMap, ParamMap } from '@angular/router';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { NavBarComponent } from '../shared/nav-bar/nav-bar.component';
import { ReviewInfoComponent } from '../shared/review-info/review-info.component';
import { MenuModule } from 'primeng/menu';
import { SplitterModule } from 'primeng/splitter';
import {  SidebarModule } from 'primeng/sidebar';
import { FooterComponent } from '../shared/footer/footer.component';
import { BreadcrumbModule } from 'primeng/breadcrumb';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { CodePanelComponent } from '../code-panel/code-panel.component';

describe('ReviewPageComponent', () => {
  let component: ReviewPageComponent;
  let fixture: ComponentFixture<ReviewPageComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      declarations: [ 
        ReviewPageComponent,
        NavBarComponent,
        ReviewInfoComponent,
        FooterComponent,
        CodePanelComponent
      ],
      imports: [
        HttpClientTestingModule,
        MenuModule,
        SplitterModule,
        SidebarModule,
        BreadcrumbModule,
        BrowserAnimationsModule
      ],
      providers: [
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ reviewId: 'test' }),
              queryParamMap: convertToParamMap({ activeApiRevisionId: 'test', diffApiRevisionId: 'test' })
            }
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