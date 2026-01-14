import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ProfilePageComponent } from './profile-page.component';
import { By } from '@angular/platform-browser';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { SharedAppModule } from 'src/app/_modules/shared/shared-app.module';
import { SignalRService } from 'src/app/_services/signal-r/signal-r.service';
import { SignalRServiceMock } from 'src/app/_services/signal-r/signal-r-test.mock';

describe('ProfilePageComponent', () => {
  let component: ProfilePageComponent;
  let fixture: ComponentFixture<ProfilePageComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ProfilePageComponent],
      imports: [
        HttpClientTestingModule,
        SharedAppModule
      ],
      providers: [
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ userNme: 'test' }),
            }
          }
        },
        { provide: SignalRService, useClass: SignalRServiceMock }
      ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ProfilePageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should NOT render reviewLanguagesListItem when userName is not in allowedApprovers', () => {
    component.userName = 'testuser';
    component.allowedApprovers = ['otheruser', 'someoneelse'];
    component.userProfile = { userName: 'testuser', preferences: {} } as any;
    component.isLoaded = true;
    fixture.detectChanges();

    const reviewLanguagesListItem = fixture.debugElement.query(By.css('#reviewLanguagesListItem'));
    expect(reviewLanguagesListItem).toBeNull();
  });
});
