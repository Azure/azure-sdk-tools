import { TestBed } from '@angular/core/testing';
import { initializeTestBed } from '../../../test-setup';
import { vi } from 'vitest';

import { NotificationsService } from './notifications.service';
import { SiteNotification } from 'src/app/_models/notificationsModel';
import { take } from 'rxjs';

// Mock IndexedDB completely for test environment
if (!global.indexedDB) {
  const storage = new Map();
  
  global.indexedDB = {
    open: vi.fn(() => {
      const request: any = {
        result: {
          transaction: vi.fn(() => ({
            objectStore: vi.fn(() => ({
              getAll: vi.fn(() => ({ 
                onsuccess: null,
                onerror: null,
                result: Array.from(storage.values()),
                addEventListener: vi.fn(),
                removeEventListener: vi.fn(),
                then: vi.fn((cb) => cb({ onsuccess: null, onerror: null, result: Array.from(storage.values()) }))
              })),
              add: vi.fn((value) => {
                const id = Date.now();
                storage.set(id, value);
                return { onsuccess: null, onerror: null, result: id, addEventListener: vi.fn(), removeEventListener: vi.fn(), then: vi.fn((cb) => cb({ result: id })) };
              }),
              put: vi.fn((value) => ({ onsuccess: null, onerror: null, addEventListener: vi.fn(), removeEventListener: vi.fn(), then: vi.fn((cb) => cb({})) })),
              delete: vi.fn((key) => {
                storage.delete(key);
                return { onsuccess: null, onerror: null, addEventListener: vi.fn(), removeEventListener: vi.fn(), then: vi.fn((cb) => cb({})) };
              }),
              clear: vi.fn(() => {
                storage.clear();
                return { onsuccess: null, onerror: null, addEventListener: vi.fn(), removeEventListener: vi.fn(), then: vi.fn((cb) => cb({})) };
              })
            }))
          })),
          close: vi.fn()
        },
        onsuccess: null,
        onerror: null,
        onupgradeneeded: null,
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
        then: vi.fn(function(cb) { 
          setTimeout(() => {
            this.onsuccess && this.onsuccess({ target: this });
            cb && cb(this);
          }, 0);
          return this;
        }),
        catch: vi.fn(function() { return this; }),
        finally: vi.fn(function() { return this; })
      };
      return request;
    }),
    deleteDatabase: vi.fn(),
    databases: vi.fn().mockResolvedValue([]),
    cmp: vi.fn()
  } as any;

  global.IDBRequest = class {} as any;
  global.IDBTransaction = class {} as any;
  global.IDBDatabase = class {} as any;
  global.IDBObjectStore = class {} as any;
  global.IDBIndex = class {} as any;
  global.IDBCursor = class {} as any;
  global.IDBKeyRange = class {} as any;
}

describe('NotificationsService', () => {
  let service: NotificationsService;

  beforeAll(() => {
    initializeTestBed();
  });

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
