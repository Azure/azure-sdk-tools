import { vi } from 'vitest';
import { Subject } from 'rxjs';

/**
 * Creates a mock SignalRService for testing.
 * Used to prevent real WebSocket connections during tests.
 */
export function createMockSignalRService() {
  return {
    startConnection: vi.fn().mockResolvedValue(undefined),
    stopConnection: vi.fn().mockResolvedValue(undefined),
    on: vi.fn(),
    off: vi.fn(),
    onNotificationUpdates: vi.fn(() => ({ pipe: vi.fn(() => ({ subscribe: vi.fn() })) })),
    onAIReviewUpdates: vi.fn(() => ({ pipe: vi.fn(() => ({ subscribe: vi.fn() })) })),
    onCommentUpdates: vi.fn(() => ({ pipe: vi.fn(() => ({ subscribe: vi.fn() })) })),
    onReviewUpdates: vi.fn(() => ({ pipe: vi.fn(() => ({ subscribe: vi.fn() })) })),
    onAPIRevisionUpdates: vi.fn(() => ({ pipe: vi.fn(() => ({ subscribe: vi.fn() })) }))
  };
}

/**
 * Creates a mock NotificationsService for testing.
 * Used to prevent actual toast notifications during tests.
 */
export function createMockNotificationsService() {
  return {
    notifications$: { subscribe: vi.fn() },
    addNotification: vi.fn(),
    clearNotification: vi.fn(),
    clearAll: vi.fn()
  };
}

/**
 * Creates a mock WorkerService for testing.
 * Used to prevent Web Worker instantiation in jsdom environment.
 */
export function createMockWorkerService() {
  return {
    startWorker: vi.fn().mockResolvedValue(undefined),
    postToApiTreeBuilder: vi.fn(),
    onMessageFromApiTreeBuilder: vi.fn(() => new Subject()),
    terminateWorker: vi.fn()
  };
}

/**
 * Creates a mock matchMedia for PrimeNG components that require it.
 * Call this in beforeAll() to set up the mock globally.
 */
export function setupMatchMediaMock() {
  if (!window.matchMedia) {
    Object.defineProperty(window, 'matchMedia', {
      writable: true,
      value: vi.fn().mockImplementation((query: string) => ({
        matches: false,
        media: query,
        onchange: null,
        addListener: vi.fn(),
        removeListener: vi.fn(),
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
        dispatchEvent: vi.fn()
      }))
    });
  }
}
