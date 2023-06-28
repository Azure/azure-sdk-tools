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
  var expectedResults = [
    {
      title: 'older revision returns correct values',
      href: 'https://apiview.dev/Assemblies/Review/b08a59aad6fe47f1949b54a531e67fa9?revisionId=ae4bb4afdc104c07a0f0058e0c133b4f&doc=False',
      reviewId: 'b08a59aad6fe47f1949b54a531e67fa9',
      revisionId: 'ae4bb4afdc104c07a0f0058e0c133b4f',
    },
    {
      title: 'older revision returns correct values 2',
      href: 'https://apiview.dev/Assemblies/Review/0ab7afb3131d4eacb1bfc1b0230fece8?revisionId=e822cfe035b148d2999a57e3e6b07460&doc=False',
      reviewId: '0ab7afb3131d4eacb1bfc1b0230fece8',
      revisionId: 'e822cfe035b148d2999a57e3e6b07460',
    },
    {
      title: 'latest revision returns correct reviewId and undefined revisionId',
      href: 'https://apiview.dev/Assemblies/Review/7674e7e8fdd0496f80b29127673928ec',
      reviewId: '7674e7e8fdd0496f80b29127673928ec',
      revisionId: undefined,
    },
    {
      title: 'latest revision returns correct reviewId and undefined revisionId 2',
      href: 'https://apiview.dev/Assemblies/Review/0ab7afb3131d4eacb1bfc1b0230fece8',
      reviewId: '0ab7afb3131d4eacb1bfc1b0230fece8',
      revisionId: undefined,
    },
    {
      title: 'review conversation page returns its reviewId',
      href: 'https://apiview.dev/Assemblies/Conversation/7c1724b222bd4a49bfeba6100d77297e',
      reviewId: '7c1724b222bd4a49bfeba6100d77297e',
      revisionId: undefined,
    },
    {
      title: 'review revisions page returns its reviewId',
      href: 'https://apiview.dev/Assemblies/Revisions/7c1724b222bd4a49bfeba6100d77297e',
      reviewId: '7c1724b222bd4a49bfeba6100d77297e',
      revisionId: undefined,
    },
    {
      title: 'review usage samples page returns its reviewId',
      href: 'https://apiview.dev/Assemblies/Samples/7c1724b222bd4a49bfeba6100d77297e',
      reviewId: '7c1724b222bd4a49bfeba6100d77297e',
      revisionId: undefined,
    },
  ];

  for (const expected of expectedResults) {
    test(`getReviewAndRevisionIdFromUrl on ${expected.title}`, async ({ page }) => {
      const testUrlString = expected.href;
      const result = hp.getReviewAndRevisionIdFromUrl(testUrlString);
      expect(result["reviewId"]).toBe(expected.reviewId);
      expect(result["revisionId"]).toBe(expected.revisionId);  
    })
  }
  
  var nonReviewExpectedResults = [
    {
      title: 'landing',
      href: 'https://apiview.dev/',
    },
    {
      title: 'login',
      href: 'https://apiview.dev/Login',
    },
    {
      title: 'profile',
      href: 'https://apiview.dev/Assemblies/Profile/yeojunh',
    },
    {
      title: 'review filter',
      href: 'https://apiview.dev/?languages=C%252B%252B&state=Closed&state=Open&status=Approved&type=Automatic&type=Manual&type=PullRequest&pageNo=1&pageSize=50',
    },
    {
      title: 'requested reviews',
      href: 'http://localhost:5000/Assemblies/RequestedReviews',
    }
  ];

  for (const expected of nonReviewExpectedResults) {
    test(`getReviewAndRevisionIdFromUrl on ${expected.title} page returns null values`, async ({ page }) => {
      const testUrlString = expected.href;
      const result = hp.getReviewAndRevisionIdFromUrl(testUrlString);
      expect(result["reviewId"]).toBe(undefined);
      expect(result["revisionId"]).toBe(undefined);
    });
  }
})