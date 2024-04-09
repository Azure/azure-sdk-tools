import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CommentThreadComponent } from './comment-thread.component';

describe('CommentThreadComponent', () => {
  let component: CommentThreadComponent;
  let fixture: ComponentFixture<CommentThreadComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      declarations: [CommentThreadComponent]
    });
    fixture = TestBed.createComponent(CommentThreadComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
