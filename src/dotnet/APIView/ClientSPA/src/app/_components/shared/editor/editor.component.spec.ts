import { ComponentFixture, TestBed } from '@angular/core/testing';

import { EditorComponent } from './editor.component';
import { EditorModule } from 'primeng/editor';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';

describe('EditorComponent', () => {
  let component: EditorComponent;
  let fixture: ComponentFixture<EditorComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      declarations: [EditorComponent],
      imports: [
        EditorModule,
        ReactiveFormsModule,
        FormsModule
      ]
    });
    fixture = TestBed.createComponent(EditorComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
