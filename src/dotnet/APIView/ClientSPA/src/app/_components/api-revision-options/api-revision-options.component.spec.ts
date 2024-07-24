import 'reflect-metadata';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ApiRevisionOptionsComponent } from './api-revision-options.component';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { ReviewPageModule } from 'src/app/_modules/review-page/review-page.module';
import { SharedAppModule } from 'src/app/_modules/shared/shared-app.module';

describe('ApiRevisionOptionsComponent', () => {
  let component: ApiRevisionOptionsComponent;
  let fixture: ComponentFixture<ApiRevisionOptionsComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      declarations: [ApiRevisionOptionsComponent],
      imports: [
        SharedAppModule,
        ReviewPageModule
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
    fixture = TestBed.createComponent(ApiRevisionOptionsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
