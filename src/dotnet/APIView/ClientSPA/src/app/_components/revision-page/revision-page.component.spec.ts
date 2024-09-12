import { ComponentFixture, TestBed } from '@angular/core/testing';

import { RevisionPageComponent } from './revision-page.component';

describe('RevisionPageComponent', () => {
  let component: RevisionPageComponent;
  let fixture: ComponentFixture<RevisionPageComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      declarations: [RevisionPageComponent]
    });
    fixture = TestBed.createComponent(RevisionPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
