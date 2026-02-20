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

    // Click the conversations icon in the side menu
    const conversationsIcon = page.locator('.bi-chat-left-dots');
    await expect(conversationsIcon).toBeVisible({ timeout: 5000 });
    await conversationsIcon.click();

    // The conversations sidebar should appear with the "Conversations" header
    const sidebarHeader = page.locator('text=Conversations').first();
    await expect(sidebarHeader).toBeVisible({ timeout: 5000 });
  });

  // TODO: Add test for comment text appearing in sidebar once mock data issue is resolved
  // The conversations component groups comments by apiRevisionId, but the grouping
  // logic isn't working correctly with the current mock setup.
});
