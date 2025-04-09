import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CrossLangViewComponent } from './cross-lang-view.component';

describe('CrossLangViewComponent', () => {
  let component: CrossLangViewComponent;
  let fixture: ComponentFixture<CrossLangViewComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CrossLangViewComponent]
    })
    .compileComponents();
    
    fixture = TestBed.createComponent(CrossLangViewComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
