import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ReviewPageOptionsComponent } from './review-page-options.component';

describe('ReviewPageOptionsComponent', () => {
  let component: ReviewPageOptionsComponent;
  let fixture: ComponentFixture<ReviewPageOptionsComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      declarations: [ReviewPageOptionsComponent]
    });
    fixture = TestBed.createComponent(ReviewPageOptionsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
