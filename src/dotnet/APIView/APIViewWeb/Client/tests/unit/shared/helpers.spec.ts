import { test, expect } from '@playwright/test';
import * as hp from "../../../src/shared/helpers";

test('getCookies return valid single cookie', async ({ page }) => {
  const testCookieString = 'preferred_color_mode=light; tz=America%2FLos_Angeles; _octo=GH1.1.1383888135.1683830525'

  // Verify that the cookies were retrieved correctly
  expect(hp.getCookieValue(testCookieString, 'preferred_color_mode')).toBe('light');
  expect(hp.getCookieValue(testCookieString, 'tz')).toBe('America%2FLos_Angeles');
  expect(hp.getCookieValue(testCookieString, '_octo')).toBe('GH1.1.1383888135.1683830525');
  expect(hp.getCookieValue(testCookieString, 'invalidCookie')).toBe(null);
});

test.describe('getReviewAndRevisionIdFromUrl should return valid review and revision id on reviews, null otherwise', () => {
  test('getReviewAndRevisionIdFromUrl on older revision returns correct values', async ({ page }) => {
    const testUrlString = 'https://apiview.dev/Assemblies/Review/b08a59aad6fe47f1949b54a531e67fa9?revisionId=ae4bb4afdc104c07a0f0058e0c133b4f&doc=False';
    const result = hp.getReviewAndRevisionIdFromUrl(testUrlString);
    expect(result["reviewId"]).toBe('b08a59aad6fe47f1949b54a531e67fa9');
    expect(result["revisionId"]).toBe('ae4bb4afdc104c07a0f0058e0c133b4f');
  
    const testUrlString2 = 'https://apiview.dev/Assemblies/Review/0ab7afb3131d4eacb1bfc1b0230fece8?revisionId=e822cfe035b148d2999a57e3e6b07460&doc=False';
    const result2 = hp.getReviewAndRevisionIdFromUrl(testUrlString2);
    expect(result2["reviewId"]).toBe('0ab7afb3131d4eacb1bfc1b0230fece8');
    expect(result2["revisionId"]).toBe('e822cfe035b148d2999a57e3e6b07460');
  }); 
  
  test('getReviewAndRevisionIdFromUrl on latest revision returns correct reviewId and undefined revisionId', async ({ page }) => {
    const testUrlString = 'https://apiview.dev/Assemblies/Review/7674e7e8fdd0496f80b29127673928ec';
    const result = hp.getReviewAndRevisionIdFromUrl(testUrlString);
    expect(result["reviewId"]).toBe('7674e7e8fdd0496f80b29127673928ec');
    expect(result["revisionId"]).toBe(undefined);
  
    const testUrlString2 = 'https://apiview.dev/Assemblies/Review/0ab7afb3131d4eacb1bfc1b0230fece8';
    const result2 = hp.getReviewAndRevisionIdFromUrl(testUrlString2);
    expect(result2["reviewId"]).toBe('0ab7afb3131d4eacb1bfc1b0230fece8');
    expect(result2["revisionId"]).toBe(undefined);
  });
  
  test('getReviewAndRevisionIdFromUrl on landing page returns null values', async ({ page }) => {
    const testUrlString = 'https://apiview.dev/';
    const result = hp.getReviewAndRevisionIdFromUrl(testUrlString);
    expect(result["reviewId"]).toBe(undefined);
    expect(result["revisionId"]).toBe(undefined);
  });
  
  test('getReviewAndRevisionIdFromUrl on profile page returns null values', async ({ page }) => {
    const testUrlString = 'https://apiview.dev/Assemblies/Profile/yeojunh';
    const result = hp.getReviewAndRevisionIdFromUrl(testUrlString);
    expect(result["reviewId"]).toBe(undefined);
    expect(result["revisionId"]).toBe(undefined);
  });
  
  test('getReviewAndRevisionIdFromUrl on login returns null values', async ({ page }) => {
    const testUrlString = 'https://apiview.dev/Login';
    const result = hp.getReviewAndRevisionIdFromUrl(testUrlString);
    expect(result["reviewId"]).toBe(undefined);
    expect(result["revisionId"]).toBe(undefined);
  });
  
  test('getReviewAndRevisionIdFromUrl on help page returns null values', async ({ page }) => {
    const testUrlString = 'https://github.com/Azure/azure-sdk-tools/blob/main/src/dotnet/APIView/APIViewWeb/README.md';
    const result = hp.getReviewAndRevisionIdFromUrl(testUrlString);
    expect(result["reviewId"]).toBe(undefined);
    expect(result["revisionId"]).toBe(undefined);
  });
  
  test('getReviewAndRevisionIdFromUrl on filtered review search page returns null values', async ({ page }) => {
    const testUrlString = 'https://apiview.dev/?languages=C%252B%252B&state=Closed&state=Open&status=Approved&type=Automatic&type=Manual&type=PullRequest&pageNo=1&pageSize=50';
    const result = hp.getReviewAndRevisionIdFromUrl(testUrlString);
    expect(result["reviewId"]).toBe(undefined);
    expect(result["revisionId"]).toBe(undefined);
  });
})