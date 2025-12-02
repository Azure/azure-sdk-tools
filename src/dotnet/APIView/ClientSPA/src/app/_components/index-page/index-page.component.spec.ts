import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { RouterTestingModule } from '@angular/router/testing';

import { IndexPageComponent } from './index-page.component';
import { NavBarComponent } from '../shared/nav-bar/nav-bar.component';
import { ReviewsListComponent } from '../reviews-list/reviews-list.component';
import { AppModule } from 'src/app/app.module';
import { SharedAppModule } from 'src/app/_modules/shared/shared-app.module';

describe('IndexPageComponent', () => {
  let component: IndexPageComponent;
  let fixture: ComponentFixture<IndexPageComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      declarations: [
        IndexPageComponent,
        NavBarComponent,
        ReviewsListComponent
      ],
      imports: [
        HttpClientTestingModule,
        RouterTestingModule,
        SharedAppModule,
        AppModule
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
