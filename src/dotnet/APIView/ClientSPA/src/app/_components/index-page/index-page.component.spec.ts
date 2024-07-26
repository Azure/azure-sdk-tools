import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';

import { IndexPageComponent } from './index-page.component';
import { NavBarComponent } from '../shared/nav-bar/nav-bar.component';
import { FooterComponent } from '../shared/footer/footer.component';
import { SplitterModule } from 'primeng/splitter';
import { ReviewsListComponent } from '../reviews-list/reviews-list.component';
import { ContextMenuModule } from 'primeng/contextmenu';
import { TableModule } from 'primeng/table';
import { SidebarModule } from 'primeng/sidebar';
import { DropdownModule } from 'primeng/dropdown';
import {  FormsModule, ReactiveFormsModule } from '@angular/forms';
import { MultiSelectModule } from 'primeng/multiselect';
import { RevisionsListComponent } from '../revisions-list/revisions-list.component';

describe('IndexPageComponent', () => {
  let component: IndexPageComponent;
  let fixture: ComponentFixture<IndexPageComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      declarations: [
        IndexPageComponent,
        NavBarComponent,
        FooterComponent,
        ReviewsListComponent,
        RevisionsListComponent
      ],
      imports: [
        HttpClientTestingModule,
        SplitterModule,
        ContextMenuModule,
        TableModule,
        SidebarModule,
        DropdownModule,
        ReactiveFormsModule,
        FormsModule,
        MultiSelectModule
      ]
    });
    fixture = TestBed.createComponent(IndexPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
