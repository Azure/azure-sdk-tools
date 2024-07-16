import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ReviewInfoComponent } from './review-info.component';
import { BreadcrumbModule } from 'primeng/breadcrumb';
import { ActivatedRoute, convertToParamMap } from '@angular/router';

describe('ReviewInfoComponent', () => {
  let component: ReviewInfoComponent;
  let fixture: ComponentFixture<ReviewInfoComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      declarations: [ReviewInfoComponent],
      imports: [
        BreadcrumbModule
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
