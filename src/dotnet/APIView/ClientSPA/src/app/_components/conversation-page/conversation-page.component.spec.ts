import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ConversationPageComponent } from './conversation-page.component';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { ReviewPageLayoutComponent } from '../shared/review-page-layout/review-page-layout.component';
import { ConversationsComponent } from '../conversations/conversations.component';
import { NavBarComponent } from '../shared/nav-bar/nav-bar.component';
import { ReviewInfoComponent } from '../shared/review-info/review-info.component';
import { MenuModule } from 'primeng/menu';
import { MenubarModule } from 'primeng/menubar';
import { TooltipModule } from 'primeng/tooltip';
import { RippleModule } from 'primeng/ripple';
import { LanguageNamesPipe } from 'src/app/_pipes/language-names.pipe';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { SidebarModule } from 'primeng/sidebar';

describe('ConversationPageComponent', () => {
  let component: ConversationPageComponent;
  let fixture: ComponentFixture<ConversationPageComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [
        ConversationPageComponent,
        ConversationsComponent,
        NavBarComponent,
        ReviewInfoComponent,
        ReviewPageLayoutComponent,
        LanguageNamesPipe,
        BrowserAnimationsModule,
        HttpClientTestingModule,
        MenuModule,
        SidebarModule,
        MenubarModule,
        TooltipModule,
        RippleModule
      ],
      providers: [
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ reviewId: 'test' })
            }
          }
        }
      ]
    });
    fixture = TestBed.createComponent(ConversationPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
