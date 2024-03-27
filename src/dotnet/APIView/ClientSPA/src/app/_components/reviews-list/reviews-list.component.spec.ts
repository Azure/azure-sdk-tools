import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ReviewsListComponent } from './reviews-list.component';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { ContextMenuModule } from 'primeng/contextmenu';
import { TableModule } from 'primeng/table';
import { SidebarModule } from 'primeng/sidebar';
import { DropdownModule } from 'primeng/dropdown';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { MultiSelectModule } from 'primeng/multiselect';

describe('ReviewsListComponent', () => {
  let component: ReviewsListComponent;
  let fixture: ComponentFixture<ReviewsListComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      declarations: [ReviewsListComponent],
      imports: [
        HttpClientTestingModule,
        ContextMenuModule,
        TableModule,
        SidebarModule,
        DropdownModule,
        ReactiveFormsModule,
        FormsModule,
        MultiSelectModule
      ]
    });
    fixture = TestBed.createComponent(ReviewsListComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
