import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ReviewPageLayoutComponent } from './review-page-layout.component';

describe('ReviewPageLayoutComponent', () => {
  let component: ReviewPageLayoutComponent;
  let fixture: ComponentFixture<ReviewPageLayoutComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      declarations: [ReviewPageLayoutComponent]
    });
    fixture = TestBed.createComponent(ReviewPageLayoutComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
