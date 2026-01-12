import { test, expect } from '@playwright/test';
import { ReviewPage } from '../../page-objects';
import { setupReviewPageMocks, setupAuthMocks } from '../../mocks/api-handlers';

test.describe('Review Page - Basic', () => {
  test.beforeEach(async ({ page }) => {
    await setupAuthMocks(page);
    await setupReviewPageMocks(page);
  });

  test('should load the review page successfully', async ({ page }) => {
    const reviewPage = new ReviewPage(page);
    await reviewPage.goto('test-review-id');
    await reviewPage.waitForReviewLoaded();

    await expect(reviewPage.codePanel).toBeVisible();
  });

  test('should display the splitter layout', async ({ page }) => {
    const reviewPage = new ReviewPage(page);
    await reviewPage.goto('test-review-id');
    await reviewPage.waitForReviewLoaded();

    await expect(reviewPage.splitter).toBeVisible();
  });

  test('should display all three main sections: left nav, code panel, and page options', async ({
    page,
  }) => {
    const reviewPage = new ReviewPage(page);

    await reviewPage.goto('test-review-id');
    await reviewPage.waitForReviewLoaded();

    await expect(reviewPage.leftNavigation).toBeVisible();
    await expect(reviewPage.codePanel).toBeVisible();
    await expect(reviewPage.pageOptions).toBeVisible();
  });

  test('should have the correct page url format', async ({ page }) => {
    const reviewPage = new ReviewPage(page);

    await reviewPage.goto('test-review-id');
    await reviewPage.waitForReviewLoaded();

    expect(page.url()).toContain('/review/test-review-id');
  });

  test('should accept activeApiRevisionId query parameter', async ({
    page,
  }) => {
    const reviewPage = new ReviewPage(page);

    await reviewPage.goto('test-review-id', 'test-revision-id');
    await reviewPage.waitForReviewLoaded();

    expect(page.url()).toContain('activeApiRevisionId=test-revision-id');
  });

  test('should load page with diff query parameters', async ({ page }) => {
    await page.goto(
      '/review/test-review-id?activeApiRevisionId=rev1&diffApiRevisionId=rev2'
    );

    await page.waitForSelector('app-review-page-layout', { timeout: 10000 });

    expect(page.url()).toContain('diffApiRevisionId=rev2');
  });

  test('should toggle page options panel visibility on and off when hamburger button is clicked', async ({
    page,
  }) => {
    const reviewPage = new ReviewPage(page);
    await reviewPage.goto('test-review-id');
    await reviewPage.waitForReviewLoaded();

    await page.route('**/api/userprofile/preference', async (route) => {
      await route.fulfill({ status: 200 });
    });

    const hamburgerButton = page.locator('label[for="page-right-panel"]');
    await expect(reviewPage.pageOptions).toBeVisible();

    // Click to hide
    await hamburgerButton.click();
    await expect(reviewPage.pageOptions).not.toBeVisible();

    // Click again to show
    await hamburgerButton.click();
    await expect(reviewPage.pageOptions).toBeVisible();
  });
});
