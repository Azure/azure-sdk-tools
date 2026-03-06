import { TestBed } from '@angular/core/testing';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { MessageService } from 'primeng/api';
import { of } from 'rxjs';
import { initializeTestBed } from '../test-setup';
import { NotificationsService } from './_services/notifications/notifications.service';
import { SignalRService } from './_services/signal-r/signal-r.service';
import { createMockSignalRService, createMockNotificationsService } from 'src/test-helpers/mock-services';
import { AppComponent } from './app.component';

describe('AppComponent', () => {
  beforeAll(() => {
    initializeTestBed();
  });

  const mockNotificationsService = {
    ...createMockNotificationsService(),
    notifications$: of([])
  };
  const mockSignalRService = createMockSignalRService();

  beforeEach(() => TestBed.configureTestingModule({
    schemas: [NO_ERRORS_SCHEMA],
    declarations: [AppComponent],
    providers: [
      MessageService,
      provideRouter([]),
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: NotificationsService, useValue: mockNotificationsService },
      { provide: SignalRService, useValue: mockSignalRService }
    ]
  }));

  it('should create the app', () => {
    const fixture = TestBed.createComponent(AppComponent);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it(`should have as title 'APIView'`, () => {
    const fixture = TestBed.createComponent(AppComponent);
    const app = fixture.componentInstance;
    expect(app.title).toEqual('APIView');
  });
});
