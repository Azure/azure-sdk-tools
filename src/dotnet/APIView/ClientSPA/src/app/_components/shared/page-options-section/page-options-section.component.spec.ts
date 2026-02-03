import { ComponentFixture, TestBed } from '@angular/core/testing';
import { initializeTestBed } from '../../../../test-setup';

import { PageOptionsSectionComponent } from './page-options-section.component';
import { PanelModule } from 'primeng/panel';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { ToggleSwitchModule } from 'primeng/toggleswitch';

describe('PageOptionsSectionComponent', () => {
  let component: PageOptionsSectionComponent;
  let fixture: ComponentFixture<PageOptionsSectionComponent>;

  beforeAll(() => {
    initializeTestBed();
  });

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [
        PageOptionsSectionComponent,
        PanelModule,
        ToggleSwitchModule,
        BrowserAnimationsModule
      ]
    });
    fixture = TestBed.createComponent(PageOptionsSectionComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
