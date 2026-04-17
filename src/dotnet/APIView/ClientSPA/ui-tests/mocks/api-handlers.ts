import { Page } from '@playwright/test';
import {
  mockReview,
  mockApiRevisions,
  mockUserProfile,
  mockComments,
  mockCodePanelData,
} from '../fixtures';

/**
 * API Mocking Handlers for E2E Tests
 */

export interface MockOptions {
  /** Delay in ms to simulate network latency */
  delay?: number;
  /** Custom review data to use instead of default */
  review?: any;
  /** Custom revisions data */
  revisions?: any;
  /** Custom user profile */
  userProfile?: any;
  /** Custom comments */
  comments?: any;
  /** Custom code panel data */
  codePanelData?: any;
  /** Simulate a failed review load */
  reviewNotFound?: boolean;
  /** Simulate a deleted review */
  reviewDeleted?: boolean;
  /** Simulate empty content (204 response) */
  emptyContent?: boolean;
}

export async function setupReviewPageMocks(
  page: Page,
  options: MockOptions = {}
): Promise<void> {
  const { delay = 0 } = options;

  await page.route('**/api/userprofile', async (route) => {
    if (route.request().method() === 'PUT') {
      // Handle profile update
      await maybeDelay(delay);
      await route.fulfill({ status: 200 });
      return;
    }
    await maybeDelay(delay);
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(options.userProfile ?? mockUserProfile),
    });
  });

  await page.route('**/api/userprofile/preference', async (route) => {
    await maybeDelay(delay);
    await route.fulfill({ status: 200 });
  });

  // IMPORTANT: Register more specific routes FIRST (Playwright matches in order)
  // Mock get review content (the binary/code content) - must be BEFORE /api/reviews/*
  await page.route('**/api/reviews/*/content*', async (route) => {
    await maybeDelay(delay);

    if (options.emptyContent) {
      await route.fulfill({ status: 204 });
      return;
    }

    await route.fulfill({
      status: 200,
      contentType: 'application/octet-stream',
      body: Buffer.from(
        JSON.stringify(options.codePanelData ?? mockCodePanelData)
      ),
    });
  });

  // Mock approvers for language
  await page.route('**/api/permissions/approvers/*', async (route) => {
    await maybeDelay(delay);
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(['approver1', 'approver2']),
    });
  });

  // Mock get review (less specific, so register AFTER content and approvers)
  await page.route('**/api/reviews/*', async (route) => {
    if (route.request().method() !== 'GET') {
      await route.continue();
      return;
    }

    await maybeDelay(delay);

    if (options.reviewNotFound) {
      await route.fulfill({ status: 404 });
      return;
    }

    const review = options.review ?? mockReview;
    if (options.reviewDeleted) {
      review.isDeleted = true;
    }

    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(review),
    });
  });

  // Mock get API revisions
  await page.route('**/api/apirevisions*', async (route) => {
    await maybeDelay(delay);
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        result: options.revisions ?? mockApiRevisions,
        pagination: {
          pageNumber: 0,
          pageSize: 50,
          totalCount: mockApiRevisions.length,
        },
      }),
    });
  });

  // Mock get comments (e.g., /api/comments/test-review-id?commentType=1)
  await page.route('**/api/comments/**', async (route) => {
    await maybeDelay(delay);
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(options.comments ?? mockComments),
    });
  });

  // Mock samples revision
  await page.route('**/api/samplesrevisions*', async (route) => {
    await maybeDelay(delay);
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([]),
    });
  });

  // Mock SignalR negotiation (return empty to skip real-time features in tests)
  await page.route('**/hubs/**', async (route) => {
    await route.fulfill({ status: 404 });
  });
}

export async function setupAuthMocks(page: Page): Promise<void> {
  await page.route('**/assets/config.json', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        apiUrl: 'https://localhost:4200/api/',
        hubUrl: 'https://localhost:4200/hubs/',
        webAppUrl: 'https://localhost:4200/',
      }),
    });
  });

  // Mock auth check - pretend user is authenticated
  await page.route('**/api/auth', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ isLoggedIn: true }),
    });
  });

  await page.route('**/api/auth/appversion', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ version: '1.0.0', hash: 'test' }),
    });
  });
}

async function maybeDelay(ms: number): Promise<void> {
  if (ms > 0) {
    await new Promise((resolve) => setTimeout(resolve, ms));
  }
}

export async function logApiCalls(page: Page): Promise<void> {
  page.on('request', (request) => {
    if (request.url().includes('/api/')) {
      console.log(`>> ${request.method()} ${request.url()}`);
    }
  });

  page.on('response', (response) => {
    if (response.url().includes('/api/')) {
      console.log(`<< ${response.status()} ${response.url()}`);
    }
  });
}
