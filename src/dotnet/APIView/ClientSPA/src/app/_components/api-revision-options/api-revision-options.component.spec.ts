import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ApiRevisionOptionsComponent } from './api-revision-options.component';

describe('ApiRevisionOptionsComponent', () => {
  let component: ApiRevisionOptionsComponent;
  let fixture: ComponentFixture<ApiRevisionOptionsComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      declarations: [ApiRevisionOptionsComponent]
    });
    fixture = TestBed.createComponent(ApiRevisionOptionsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
