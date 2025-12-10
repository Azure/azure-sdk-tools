import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CrossLangViewComponent } from './cross-lang-view.component';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';

describe('CrossLangViewComponent', () => {
  let component: CrossLangViewComponent;
  let fixture: ComponentFixture<CrossLangViewComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
    declarations: [CrossLangViewComponent],
    imports: [],
    providers: [provideHttpClient(withInterceptorsFromDi()), provideHttpClientTesting()]
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
