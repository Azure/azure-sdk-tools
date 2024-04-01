import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CodePanelComponent } from './code-panel.component';

describe('CodePanelComponent', () => {
  let component: CodePanelComponent;
  let fixture: ComponentFixture<CodePanelComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      declarations: [CodePanelComponent]
    });
    fixture = TestBed.createComponent(CodePanelComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
