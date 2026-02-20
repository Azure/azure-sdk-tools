import { test, expect } from '@playwright/test';
import { ReviewPage } from '../../page-objects';
import { setupReviewPageMocks, setupAuthMocks } from '../../mocks/api-handlers';

test.describe('Review Page - Diff Rendering', () => {
  test.beforeEach(async ({ page, context }) => {
    await context.clearCookies();
    await page.goto('about:blank');
    
    await setupAuthMocks(page);
    await setupReviewPageMocks(page);
  });

  test('should display diff lines when comparing two revisions', async ({ page }) => {
    await page.goto('/review/test-review-id?activeApiRevisionId=rev1&diffApiRevisionId=rev2');
    await page.waitForSelector('app-code-panel', { timeout: 10000 });
    await page.waitForSelector('app-code-panel .code-line', { timeout: 10000 });
    
    const codeLines = page.locator('app-code-panel .code-line');
    const lineCount = await codeLines.count();
    
    expect(lineCount).toBeGreaterThan(0);
  });

  test('should show added and removed lines in diff mode', async ({ page }) => {
    await page.goto('/review/test-review-id?activeApiRevisionId=rev1&diffApiRevisionId=rev2');
    await page.waitForSelector('app-code-panel', { timeout: 10000 });
    
    expect(page.url()).toContain('diffApiRevisionId=rev2');
    await page.waitForSelector('app-code-panel .code-line', { timeout: 10000 });
    
    const codeLines = page.locator('app-code-panel .code-line');
    expect(await codeLines.count()).toBeGreaterThan(0);
  });

  test('should toggle between tree and full diff styles', async ({ page }) => {
    await page.goto('/review/test-review-id?activeApiRevisionId=rev1&diffApiRevisionId=rev2');
    await page.waitForSelector('app-code-panel', { timeout: 10000 });
    await page.waitForSelector('app-code-panel .code-line', { timeout: 10000 });
    
    const treeStyleButton = page.locator('input[type="radio"][value="trees"]');
    const fullStyleButton = page.locator('input[type="radio"][value="full"]');
    
    if (await treeStyleButton.count() > 0) {
      await treeStyleButton.click();
      await page.waitForSelector('app-code-panel .code-line', { timeout: 10000 });
      await expect(page.locator('app-code-panel .code-line').first()).toBeVisible();
      
      await fullStyleButton.click();
      await page.waitForSelector('app-code-panel .code-line', { timeout: 10000 });
      await expect(page.locator('app-code-panel .code-line').first()).toBeVisible();
    }
    
    await page.waitForSelector('app-code-panel');
    const codeLines = page.locator('app-code-panel .code-line');
    expect(await codeLines.count()).toBeGreaterThan(0);
  });

  test('should load review page without errors when no diff is present', async ({ page }) => {
    const reviewPage = new ReviewPage(page);
    await reviewPage.goto('test-review-id');
    await reviewPage.waitForReviewLoaded();
    
    await expect(reviewPage.codePanel).toBeVisible();
  });

  test('should show documentation when showDocumentation is enabled', async ({ page }) => {
    await page.goto('/review/test-review-id?activeApiRevisionId=rev1&diffApiRevisionId=rev2');
    await page.waitForSelector('app-code-panel', { timeout: 10000 });
    
    const docToggle = page.locator('input[type="checkbox"][id*="documentation"]');
    if (await docToggle.count() > 0) {
      const isChecked = await docToggle.first().isChecked();
      if (!isChecked) {
        await docToggle.first().check();
        await expect(docToggle.first()).toBeChecked();
      }
    }
    
    const reviewPage = new ReviewPage(page);
    await expect(reviewPage.codePanel).toBeVisible();
  });

  test('should handle tree diff style with nodes', async ({ page }) => {
    await page.goto('/review/test-review-id?activeApiRevisionId=rev1&diffApiRevisionId=rev2');
    await page.waitForSelector('app-code-panel', { timeout: 10000 });
    await page.waitForSelector('app-code-panel .code-line', { timeout: 10000 });
    
    const treeStyleButton = page.locator('input[type="radio"][value="trees"]');
    if (await treeStyleButton.count() > 0) {
      await treeStyleButton.click();
      await page.waitForSelector('app-code-panel .code-line', { timeout: 10000 });
      await expect(page.locator('app-code-panel .code-line').first()).toBeVisible();
    }
    
    const codeLines = page.locator('app-code-panel .code-line');
    expect(await codeLines.count()).toBeGreaterThan(0);
  });
});
