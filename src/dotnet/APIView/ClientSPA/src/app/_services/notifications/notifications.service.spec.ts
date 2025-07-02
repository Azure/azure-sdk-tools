import { TestBed } from '@angular/core/testing';

import { NotificationsService } from './notifications.service';
import { SiteNotification } from 'src/app/_models/notificationsModel';
import { take } from 'rxjs';

describe('NotificationsService', () => {
  let service: NotificationsService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(NotificationsService);
    service.clearAll();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should not add notifications with duplicate data', (done) => {
    const not1 = new SiteNotification('review1', 'rev1', 'Title', 'Message', 'info', new Date());
    const not2 = new SiteNotification('review1', 'rev1', 'Title', 'Message', 'info', not1.createdOn);

    service.addNotification(not1).then(() => {
      service.addNotification(not2).then(() => {
        service.notifications$.pipe(take(1)).subscribe({
          next: notifications => {
            expect(notifications.length).toBe(1);
            done();
          },
          error: err => done.fail(err)
        });
      });
    });
  });
});
