import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ReviewsListComponent } from './reviews-list.component';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { SharedAppModule } from 'src/app/_modules/shared/shared-app.module';
import { AppModule } from 'src/app/app.module';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';

describe('ReviewsListComponent', () => {
  let component: ReviewsListComponent;
  let fixture: ComponentFixture<ReviewsListComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
    declarations: [ReviewsListComponent],
    imports: [SharedAppModule,
        AppModule],
    providers: [provideHttpClient(withInterceptorsFromDi()), provideHttpClientTesting()]
});
    fixture = TestBed.createComponent(ReviewsListComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
