import { test, expect } from '@playwright/test';
import { ReviewPage } from '../../page-objects';
import { setupAuthMocks, setupReviewPageMocks } from '../../mocks/api-handlers';
import { mockComments } from '../../fixtures';

test.describe('Conversations Side Panel', () => {
  test.beforeEach(async ({ page }) => {
    await setupAuthMocks(page);
    await setupReviewPageMocks(page, { comments: mockComments });
  });

  test('should open conversations side panel when clicking chat icon', async ({ page }) => {
    const reviewPage = new ReviewPage(page);
    await reviewPage.goto('test-review-id');
    await reviewPage.waitForReviewLoaded();

    // Click the Comments tab in the horizontal navigation
    const commentsTab = page.locator('.page-tab', { hasText: 'Comments' });
    await expect(commentsTab).toBeVisible({ timeout: 5000 });
    await commentsTab.click();

    // The conversations page should appear (URL changes to /conversations)
    await expect(page).toHaveURL(/conversations/, { timeout: 5000 });
  });

  // TODO: Add test for comment text appearing in sidebar once mock data issue is resolved
  // The conversations component groups comments by apiRevisionId, but the grouping
  // logic isn't working correctly with the current mock setup.
});
