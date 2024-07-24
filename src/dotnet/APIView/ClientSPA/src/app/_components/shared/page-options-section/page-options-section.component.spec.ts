import { ComponentFixture, TestBed } from '@angular/core/testing';

import { PageOptionsSectionComponent } from './page-options-section.component';
import { PanelModule } from 'primeng/panel';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { InputSwitchModule } from 'primeng/inputswitch';

describe('PageOptionsSectionComponent', () => {
  let component: PageOptionsSectionComponent;
  let fixture: ComponentFixture<PageOptionsSectionComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      declarations: [PageOptionsSectionComponent],
      imports: [
        PanelModule,
        InputSwitchModule,
        BrowserAnimationsModule
      ]
    });
    fixture = TestBed.createComponent(PageOptionsSectionComponent);
    component = fixture.componentInstance;
    component.sectionName = 'Test Section';
    component.collapsedInput = false
    component.sectionId = 'Test Id'
    component.collapsed = false;
    component.sectionStateCookieKey = 'Test Key';
  
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
