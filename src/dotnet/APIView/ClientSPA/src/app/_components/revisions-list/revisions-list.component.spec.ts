import { ComponentFixture, TestBed } from '@angular/core/testing';

import { RevisionsListComponent } from './revisions-list.component';

describe('RevisionListComponent', () => {
  let component: RevisionsListComponent;
  let fixture: ComponentFixture<RevisionsListComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      declarations: [RevisionsListComponent]
    });
    fixture = TestBed.createComponent(RevisionsListComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
