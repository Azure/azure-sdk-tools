import { ComponentFixture, TestBed } from '@angular/core/testing';
import { initializeTestBed } from '../../../test-setup';

import { ReviewNavComponent } from './review-nav.component';

describe('ReviewNavComponent', () => {
  let component: ReviewNavComponent;
  let fixture: ComponentFixture<ReviewNavComponent>;

  beforeAll(() => {
    initializeTestBed();
  });

  beforeEach(() => {
    TestBed.configureTestingModule({
      declarations: [ReviewNavComponent]
    });
    fixture = TestBed.createComponent(ReviewNavComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
