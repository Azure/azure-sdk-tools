import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';

import { IndexPageComponent } from './index-page.component';
import { AppModule } from 'src/app/app.module';
import { SharedAppModule } from 'src/app/_modules/shared/shared-app.module';

describe('IndexPageComponent', () => {
  let component: IndexPageComponent;
  let fixture: ComponentFixture<IndexPageComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      declarations: [
        IndexPageComponent
      ],
      imports: [
        HttpClientTestingModule,
        SharedAppModule,
        AppModule
      ]
    });
    fixture = TestBed.createComponent(IndexPageComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
