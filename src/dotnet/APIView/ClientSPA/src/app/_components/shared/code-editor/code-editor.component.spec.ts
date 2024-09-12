import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CodeEditorComponent } from './code-editor.component';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { of } from 'rxjs';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { MonacoEditorModule } from 'ngx-monaco-editor-v2';
import { ReviewNavComponent } from '../../review-nav/review-nav.component';
import { FormsModule } from '@angular/forms';

describe('CodeEditorComponent', () => {
  let component: CodeEditorComponent;
  let fixture: ComponentFixture<CodeEditorComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      declarations: [
        CodeEditorComponent,
        ReviewNavComponent
      ],
      imports: [
        HttpClientTestingModule,
        FormsModule,
        MonacoEditorModule.forRoot()
      ],
      providers: [
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
