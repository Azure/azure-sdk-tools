import { Injectable } from '@angular/core';
import { IDBPDatabase, openDB } from 'idb';
import { BehaviorSubject } from 'rxjs';
import { INDEXED_DB_NAME } from 'src/app/_helpers/common-helpers';
import { SiteNotification, NotificationsDb } from 'src/app/_models/notificationsModel';

const NOTIFICATIONS_TABLE_NAME = 'notifications';

@Injectable({
  providedIn: 'root'
})
export class NotificationsService {
  private dbPromise: Promise<IDBPDatabase<NotificationsDb>>
  private notificationsSubject = new BehaviorSubject<SiteNotification[]>([]);
  notifications$ = this.notificationsSubject.asObservable();

  constructor() {
    this.dbPromise = openDB<NotificationsDb>(INDEXED_DB_NAME, 1, {
      upgrade(db) {
        db.createObjectStore(NOTIFICATIONS_TABLE_NAME, { keyPath: 'id' })
      },
    });
    this.loadNotifications();
  }

  async addNotification(notification: SiteNotification) {
    const db = await this.dbPromise;
    await db.put(NOTIFICATIONS_TABLE_NAME, notification);
    this.loadNotifications();
  }

  async clearNotification(id: string) {
    const db = await this.dbPromise;
    await db.delete(NOTIFICATIONS_TABLE_NAME, id);
    this.loadNotifications();
  }

  async clearAll() {
    const db = await this.dbPromise;
    await db.clear(NOTIFICATIONS_TABLE_NAME);
    this.loadNotifications();
  }

  private async loadNotifications() {
    const db = await this.dbPromise;
    const all = await db.getAll(NOTIFICATIONS_TABLE_NAME);
    this.notificationsSubject.next(all.sort((a, b) => b.createdOn.getTime() - a.createdOn.getTime()));
  }
}
