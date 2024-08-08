import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ConversationsComponent } from './conversations.component';

describe('ConversationComponent', () => {
  let component: ConversationsComponent;
  let fixture: ComponentFixture<ConversationsComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      declarations: [ConversationsComponent]
    });
    fixture = TestBed.createComponent(ConversationsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
