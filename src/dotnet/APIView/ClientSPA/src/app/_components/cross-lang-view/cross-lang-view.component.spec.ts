import { ComponentFixture, TestBed } from '@angular/core/testing';
import { initializeTestBed } from '../../../test-setup';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';

import { CrossLangViewComponent } from './cross-lang-view.component';

describe('CrossLangViewComponent', () => {
  let component: CrossLangViewComponent;
  let fixture: ComponentFixture<CrossLangViewComponent>;

  beforeAll(() => {
    initializeTestBed();
  });

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [
        CrossLangViewComponent
      ],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
      ]
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
