import { test, expect } from '@playwright/test';
import * as hp from "../../src/shared/helpers";

test('getCookies return valid single cookie', async ({ page }) => {
  const testCookieString = 'preferred_color_mode=light; tz=America%2FLos_Angeles; _octo=GH1.1.1383888135.1683830525'

  // Verify that the cookies were retrieved correctly
  expect(hp.getCookieValue(testCookieString, 'preferred_color_mode')).toBe('light');
  expect(hp.getCookieValue(testCookieString, 'tz')).toBe('America%2FLos_Angeles');
  expect(hp.getCookieValue(testCookieString, '_octo')).toBe('GH1.1.1383888135.1683830525');
  expect(hp.getCookieValue(testCookieString, 'invalidCookie')).toBe(null);
});