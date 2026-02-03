import { ComponentFixture, TestBed } from '@angular/core/testing';
import { initializeTestBed } from '../../../../test-setup';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of } from 'rxjs';

import { CodeEditorComponent } from './code-editor.component';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { MonacoEditorModule } from 'ngx-monaco-editor-v2';
import { ReviewNavComponent } from '../../review-nav/review-nav.component';
import { FormsModule } from '@angular/forms';

describe('CodeEditorComponent', () => {
  let component: CodeEditorComponent;
  let fixture: ComponentFixture<CodeEditorComponent>;

  beforeAll(() => {
    initializeTestBed();
  });

  beforeEach(() => {
    TestBed.configureTestingModule({
      declarations: [
        CodeEditorComponent,
        ReviewNavComponent
      ],
      imports: [
        FormsModule,
        MonacoEditorModule.forRoot()
      ],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ test: 'test' }),
            },
            queryParams: of(convertToParamMap({ test: 'test' }))
          },
        }
      ]
    });
    fixture = TestBed.createComponent(CodeEditorComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
