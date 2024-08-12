import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ConversationsComponent } from './conversations.component';
import { SharedAppModule } from 'src/app/_modules/shared/shared-app.module';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { ReviewPageModule } from 'src/app/_modules/review-page/review-page.module';

describe('ConversationComponent', () => {
  let component: ConversationsComponent;
  let fixture: ComponentFixture<ConversationsComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      declarations: [ConversationsComponent],
      imports: [
        HttpClientTestingModule,
        ReviewPageModule,
        SharedAppModule
      ],
    });
    fixture = TestBed.createComponent(ConversationsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
