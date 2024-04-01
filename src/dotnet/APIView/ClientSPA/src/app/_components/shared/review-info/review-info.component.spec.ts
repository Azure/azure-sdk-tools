import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ReviewInfoComponent } from './review-info.component';

describe('ReviewInfoComponent', () => {
  let component: ReviewInfoComponent;
  let fixture: ComponentFixture<ReviewInfoComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      declarations: [ReviewInfoComponent]
    });
    fixture = TestBed.createComponent(ReviewInfoComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
