// Global test setup for Vitest
// Mocks for browser APIs not available in jsdom

import { vi } from 'vitest';

// Mock @microsoft/signalr to prevent 'hubs/notification' connection errors
// This must be at the top level to be hoisted
vi.mock('@microsoft/signalr', () => ({
  HubConnectionBuilder: class {
    withUrl() { return this; }
    configureLogging() { return this; }
    withAutomaticReconnect() { return this; }
    build() {
      return {
        start: vi.fn().mockResolvedValue(undefined),
        stop: vi.fn().mockResolvedValue(undefined),
        on: vi.fn(),
        off: vi.fn(),
        invoke: vi.fn().mockResolvedValue(undefined)
      };
    }
  },
  LogLevel: {
    Information: 3
  }
}));

// Mock matchMedia FIRST - before Angular loads
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

// Mock indexedDB early - must be set before any modules import 'idb'
if (typeof indexedDB === 'undefined') {
  const mockObjectStore = {
    add: vi.fn().mockResolvedValue(undefined),
    get: vi.fn().mockResolvedValue(undefined),
    getAll: vi.fn().mockResolvedValue([]),
    delete: vi.fn().mockResolvedValue(undefined),
    clear: vi.fn().mockResolvedValue(undefined),
    put: vi.fn().mockResolvedValue(undefined)
  };

  const mockTransaction = {
    objectStore: vi.fn(() => mockObjectStore),
    done: Promise.resolve()
  };

  const mockDB = {
    createObjectStore: vi.fn(() => mockObjectStore),
    transaction: vi.fn(() => mockTransaction),
    close: vi.fn()
  };

  (globalThis as any).indexedDB = {
    open: vi.fn(() => {
      const request = {
        result: mockDB,
        onsuccess: null,
        onerror: null,
        onupgradeneeded: null
      };
      setTimeout(() => {
        if (request.onupgradeneeded) {
          request.onupgradeneeded({ target: request } as any);
        }
        if (request.onsuccess) {
          request.onsuccess({ target: request } as any);
        }
      }, 0);
      return request as any;
    }),
    deleteDatabase: vi.fn(() => ({
      onsuccess: null,
      onerror: null
    }))
  };
}

// Mock Worker (used by WorkerService)
if (typeof Worker === 'undefined') {
  (globalThis as any).Worker = class Worker {
    constructor(public url: string | URL) {}
    postMessage = vi.fn();
    terminate = vi.fn();
    onmessage: ((this: Worker, ev: MessageEvent) => any) | null = null;
    onerror: ((this: Worker, ev: ErrorEvent) => any) | null = null;
    addEventListener = vi.fn();
    removeEventListener = vi.fn();
    dispatchEvent = vi.fn();
  } as any;
}

// Mock Clipboard API
const mockClipboard = {
  writeText: vi.fn().mockResolvedValue(undefined),
  readText: vi.fn().mockResolvedValue(''),
  write: vi.fn().mockResolvedValue(undefined),
  read: vi.fn().mockResolvedValue([])
};

Object.defineProperty(navigator, 'clipboard', {
  value: mockClipboard,
  writable: true,
  configurable: true
});

// Mock ngx-simplemde to prevent ESM import errors
// This allows SharedAppModule and other modules to import it without breaking
vi.mock('ngx-simplemde', () => ({
  SimplemdeModule: class {},
  SimplemdeOptions: class {},
  SimplemdeComponent: class {
    value = '';
    options = {};
    valueChange = { emit: vi.fn() };
  }
}));

// Mock EditorComponent to avoid SimpleMDE loading issues in tests
// The real component uses SimpleMDE in the app, but tests use this stub
vi.mock('src/app/_components/shared/editor/editor.component', () => ({
  EditorComponent: class {
    content = '';
    contentChange = { emit: vi.fn() };
    editorId = '';
    editorOptions = {};
    
    onContentChange(newContent) {
      this.content = newContent;
      this.contentChange.emit(newContent);
    }
    
    getEditorContent() {
      return this.content;
    }
    
    ngAfterViewInit() {
      // Stub - no-op in tests
    }
  }
}));

// Mock SignalR Service to prevent real connection attempts during component imports
vi.mock('src/app/_services/signal-r/signal-r.service', () => ({
  SignalRService: class {
    startConnection = vi.fn().mockResolvedValue(undefined);
    stopConnection = vi.fn();
    on = vi.fn();
    off = vi.fn();
    invoke = vi.fn().mockResolvedValue(undefined);
    onNotificationUpdates = vi.fn(() => ({ pipe: vi.fn(() => ({ subscribe: vi.fn() })) }));
    onAIReviewUpdates = vi.fn(() => ({ pipe: vi.fn(() => ({ subscribe: vi.fn() })) }));
    onCommentUpdates = vi.fn(() => ({ pipe: vi.fn(() => ({ subscribe: vi.fn() })) }));
    onReviewUpdates = vi.fn(() => ({ pipe: vi.fn(() => ({ subscribe: vi.fn() })) }));
    onAPIRevisionUpdates = vi.fn(() => ({ pipe: vi.fn(() => ({ subscribe: vi.fn() })) }));
    pushCommentUpdates = vi.fn();
  }
}));

