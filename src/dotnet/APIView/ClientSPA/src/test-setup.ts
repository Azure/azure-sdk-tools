// Angular test setup helper for Vitest
import { getTestBed } from '@angular/core/testing';
import {
  BrowserDynamicTestingModule,
  platformBrowserDynamicTesting,
} from '@angular/platform-browser-dynamic/testing';


export function initializeTestBed(): void {
  try {
    getTestBed().initTestEnvironment(
      BrowserDynamicTestingModule,
      platformBrowserDynamicTesting(),
      {
        errorOnUnknownElements: true,
        errorOnUnknownProperties: true,
      }
    );
  } catch (error) {
    console.warn('[test-setup] TestBed already initialized, continuing...');
  }
}
