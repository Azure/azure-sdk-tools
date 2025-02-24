import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ReviewInfoComponent } from './review-info.component';
import { BreadcrumbModule } from 'primeng/breadcrumb';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { MenubarModule } from 'primeng/menubar';
import { LanguageNamesPipe } from 'src/app/_pipes/language-names.pipe';
import { RevisionOptionsComponent } from '../../revision-options/revision-options.component';
import { DropdownModule } from 'primeng/dropdown';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';

describe('ReviewInfoComponent', () => {
  let component: ReviewInfoComponent;
  let fixture: ComponentFixture<ReviewInfoComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      declarations: [
        ReviewInfoComponent,
        RevisionOptionsComponent,
        LanguageNamesPipe
      ],
      imports: [
        BreadcrumbModule,
        MenubarModule,
        DropdownModule,
        ReactiveFormsModule,
        FormsModule
      ],
      providers: [
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ reviewId: 'test' }),
              queryParamMap: convertToParamMap({ activeApiRevisionId: 'test', diffApiRevisionId: 'test' })
            }
          }
        }
      ]
    });
    fixture = TestBed.createComponent(ReviewInfoComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
