import { test, expect } from '@playwright/test';
import { ReviewPage } from '../../page-objects';
import { setupReviewPageMocks, setupAuthMocks } from '../../mocks/api-handlers';

test.describe('Code Panel - Loading States', () => {
  test('should show loading spinner while fetching content', async ({
    page,
  }) => {
    await setupAuthMocks(page);
    await setupReviewPageMocks(page, { delay: 2000 });

    await page.goto('/review/test-review-id');

    const loadingSpinner = page.locator(
      'app-code-panel .spinner-border[role="status"]'
    );
    await expect(loadingSpinner).toBeVisible({ timeout: 5000 });

    const loadingText = page.locator(
      'app-code-panel .spinner-border .visually-hidden'
    );
    await expect(loadingText).toHaveText('Loading...');

    await expect(loadingSpinner).not.toBeVisible({ timeout: 10000 });
    await expect(page.locator('.code-line').first()).toBeVisible();
  });

  test('should show error message when content fails to load', async ({
    page,
  }) => {
    await setupAuthMocks(page);
    await setupReviewPageMocks(page, { emptyContent: true });

    const reviewPage = new ReviewPage(page);
    await reviewPage.goto('test-review-id');
    await page.waitForSelector('app-code-panel .spinner-border', {
      state: 'hidden',
      timeout: 10000,
    });

    // Verify the error message is displayed (h5 element with loadFailedMessage)
    const errorMessage = page.locator('app-code-panel h5');
    await expect(errorMessage).toBeVisible();

    const messageText = await errorMessage.textContent();
    expect(messageText).toBeTruthy();
    expect(messageText!.length).toBeGreaterThan(10);
  });
});

test.describe('Code Panel - Responsive Layout', () => {
  test('should adjust to different viewport sizes', async ({ page }) => {
    await setupAuthMocks(page);
    await setupReviewPageMocks(page);

    const reviewPage = new ReviewPage(page);
    await reviewPage.goto('test-review-id');
    await reviewPage.waitForReviewLoaded();

    await page.setViewportSize({ width: 1024, height: 768 });
    await expect(reviewPage.codePanel).toBeVisible();

    await page.setViewportSize({ width: 1920, height: 1080 });
    await expect(reviewPage.codePanel).toBeVisible();
  });

  test('should maintain panel structure on resize', async ({ page }) => {
    await setupAuthMocks(page);
    await setupReviewPageMocks(page);

    const reviewPage = new ReviewPage(page);
    await reviewPage.goto('test-review-id');
    await reviewPage.waitForReviewLoaded();

    const initialPanelCount = await page.locator('.p-splitter-panel').count();

    await page.setViewportSize({ width: 800, height: 600 });
    await expect(reviewPage.codePanel).toBeVisible({ timeout: 5000 });

    const afterResizePanelCount = await page
      .locator('.p-splitter-panel')
      .count();

    // Panel count should remain the same
    expect(afterResizePanelCount).toBe(initialPanelCount);
  });
});
