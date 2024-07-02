import { ComponentFixture, TestBed } from '@angular/core/testing';

import { PageOptionsSectionComponent } from './page-options-section.component';

describe('PageOptionsSectionComponent', () => {
  let component: PageOptionsSectionComponent;
  let fixture: ComponentFixture<PageOptionsSectionComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      declarations: [PageOptionsSectionComponent]
    });
    fixture = TestBed.createComponent(PageOptionsSectionComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
