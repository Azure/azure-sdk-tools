import 'reflect-metadata';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ApiRevisionOptionsComponent } from './api-revision-options.component';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { DropdownModule } from 'primeng/dropdown';
import { FormsModule } from '@angular/forms';

describe('ApiRevisionOptionsComponent', () => {
  let component: ApiRevisionOptionsComponent;
  let fixture: ComponentFixture<ApiRevisionOptionsComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      declarations: [ApiRevisionOptionsComponent],
      imports: [
        DropdownModule,
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
    fixture = TestBed.createComponent(ApiRevisionOptionsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
