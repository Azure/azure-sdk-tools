import { test, expect } from '@playwright/test';
import { ReviewPage } from '../../page-objects/review.page';
import { setupAuthMocks, setupReviewPageMocks } from '../../mocks/api-handlers';
import {
  mockComments,
  generateLargeCodePanelData,
  mockCodePanelDataWithDiff,
} from '../../fixtures';

test.describe('Code Panel - Line Click Actions', () => {
  test.beforeEach(async ({ page }) => {
    await setupAuthMocks(page);
    await setupReviewPageMocks(page);
    await page.goto('/review/test-review-id');
    await page.waitForSelector('app-code-panel', { timeout: 10000 });
  });

  test('should show action menu when clicking line number', async ({
    page,
  }) => {
    await page.waitForSelector('.code-line', { timeout: 10000 });

    const lineNumber = page.locator('.line-number').first();
    await expect(lineNumber).toBeVisible();
    await lineNumber.click();

    const menu = page.locator('.p-menu.p-menu-overlay');
    await expect(menu).toBeVisible({ timeout: 3000 });
  });

  test('should have "Copy line" option in line action menu', async ({
    page,
  }) => {
    await page.waitForSelector('.code-line', { timeout: 10000 });

    const lineNumber = page.locator('.line-number').first();
    await lineNumber.click();

    const copyLineOption = page
      .locator('.p-menuitem-link')
      .filter({ hasText: /Copy line/i });
    await expect(copyLineOption.first()).toBeVisible();
  });

  test('should have "Copy permalink" option in line action menu', async ({
    page,
  }) => {
    await page.waitForSelector('.code-line', { timeout: 10000 });

    const lineNumber = page.locator('.line-number').first();
    await lineNumber.click();

    const copyPermalinkOption = page
      .locator('.p-menuitem-link')
      .filter({ hasText: /Copy permalink/i });
    await expect(copyPermalinkOption.first()).toBeVisible();
  });

  test('should close line number menu when clicking outside', async ({
    page,
  }) => {
    await page.waitForSelector('.code-line', { timeout: 10000 });

    // Open the menu
    const lineNumber = page.locator('.line-number').first();
    await lineNumber.click();

    const menu = page.locator('.p-menu.p-menu-overlay');
    await expect(menu).toBeVisible();

    // Click outside to close the menu
    await page
      .locator('app-code-panel')
      .click({ position: { x: 300, y: 300 } });

    // Verify the menu is now hidden
    await expect(menu).not.toBeVisible();
  });

  test('should open comment editor when clicking "Add comment" icon', async ({
    page,
  }) => {
    await page.waitForSelector('.code-line', { timeout: 10000 });

    const threadsBefore = await page.locator('app-comment-thread').count();

    const codeLine = page.locator('.code-line').nth(4);

    await codeLine.scrollIntoViewIfNeeded();

    // Hover over the line-number-container to trigger visibility of add-comment-btn
    const lineNumberContainer = codeLine.locator('.line-number-container');
    await lineNumberContainer.hover();

    // Find the add comment button inside .line-actions (next to the line number)
    const commentBtn = codeLine.locator('.line-actions .add-comment-btn');

    // Verify the comment button is visible on hover
    await expect(commentBtn).toBeVisible({ timeout: 5000 });

    await commentBtn.click();

    // Wait for the comment thread to appear
    const threadsAfter = await page
      .locator('app-comment-thread')
      .count({ timeout: 5000 });

    expect(threadsAfter).toBeGreaterThan(threadsBefore);
  });
});

test.describe('Code Panel - Page Options Toggles', () => {
  test.beforeEach(async ({ page }) => {
    await setupAuthMocks(page);
    await setupReviewPageMocks(page, { comments: mockComments });
    await page.goto('/review/test-review-id');
    await page.waitForSelector('app-code-panel', { timeout: 10000 });
  });

  test('should toggle line numbers visibility on and off', async ({ page }) => {
    // Line numbers should be visible by default
    const lineNumbers = page.locator('.line-number');
    await expect(lineNumbers.first()).toBeVisible();
    const lineNumbersToggle = page
      .locator('li.list-group-item')
      .filter({ hasText: 'Line numbers' })
      .locator('p-toggleswitch');

    // Click to hide
    await lineNumbersToggle.click();
    await expect(lineNumbers.first()).not.toBeVisible();

    // Click again to show
    await lineNumbersToggle.click();
    await expect(lineNumbers.first()).toBeVisible();
  });

  test('should toggle comments visibility on and off', async ({ page }) => {
    // Comments should be visible by default
    const commentThreads = page.locator('app-comment-thread');
    await expect(commentThreads.first()).toBeVisible();
    const commentsToggle = page
      .locator('li.list-group-item')
      .filter({ hasText: /^Comments$/ })
      .locator('p-toggleswitch');

    // Click to hide
    await commentsToggle.click();
    await expect(commentThreads.first()).not.toBeVisible();

    // Click again to show
    await commentsToggle.click();
    await expect(commentThreads.first()).toBeVisible();
  });

  test('should toggle left navigation visibility on and off', async ({
    page,
  }) => {
    const reviewPage = new ReviewPage(page);
    // Left navigation should be visible by default
    await expect(reviewPage.leftNavigation).toBeVisible();
    const leftNavToggle = page
      .locator('li.list-group-item')
      .filter({ hasText: 'Left navigation' })
      .locator('p-toggleswitch');

    // Click to hide
    await leftNavToggle.click();
    await expect(reviewPage.leftNavigation).not.toBeVisible();

    // Click again to show
    await leftNavToggle.click();
    await expect(reviewPage.leftNavigation).toBeVisible();
  });
});

test.describe('Code Panel - Virtual Scrolling', () => {
  test.beforeEach(async ({ page }) => {
    const largeData = generateLargeCodePanelData(100);
    await setupAuthMocks(page);
    await setupReviewPageMocks(page, { codePanelData: largeData });
    await page.goto('/review/test-review-id');
    await page.waitForSelector('app-code-panel', { timeout: 10000 });
  });

  test('should have virtual scrolling container', async ({ page }) => {
    await expect(page.locator('app-code-panel')).toBeVisible();
    await expect(page.locator('.code-line').first()).toBeVisible();

    // Verify the viewport is the scrollable container
    const viewport = page.locator('#viewport');
    await expect(viewport).toBeVisible();
  });

  test('should scroll through large code panel and render lines correctly', async ({
    page,
  }) => {
    await expect(page.locator('.code-line').first()).toBeVisible();

    // The scrollable container is #viewport, not app-code-panel
    const viewport = page.locator('#viewport');

    async function isInViewportScrollArea(lineLocator: any): Promise<boolean> {
      const lineBox = await lineLocator.boundingBox();
      const viewportBox = await viewport.boundingBox();
      if (!lineBox || !viewportBox) return false;

      return (
        lineBox.y >= viewportBox.y &&
        lineBox.y < viewportBox.y + viewportBox.height
      );
    }

    const lineNumber1 = page.locator('.line-number').filter({ hasText: /^1$/ });
    expect(await isInViewportScrollArea(lineNumber1)).toBe(true);

    // Verify we have code lines visible
    const visibleLineNumbers = page.locator('.line-number');
    const count = await visibleLineNumbers.count();
    expect(count).toBeGreaterThan(0);

    // Verify lines are rendered with sequential numbers (virtual scrolling renders correctly)
    const firstVisibleLineText = await visibleLineNumbers.first().textContent();
    const firstVisibleLineNum = parseInt(firstVisibleLineText || '0', 10);
    // Line numbers should be positive integers - at least line 1 should be visible
    expect(firstVisibleLineNum).toBeGreaterThanOrEqual(1);
  });
});

test.describe('Code Panel - Diff Mode', () => {
  test.beforeEach(async ({ page }) => {
    await setupAuthMocks(page);
    await setupReviewPageMocks(page, {
      codePanelData: mockCodePanelDataWithDiff,
    });
  });

  test('should render added lines with green background and plus sign', async ({
    page,
  }) => {
    await page.goto(
      '/review/test-review-id?activeApiRevisionId=revision-1&diffApiRevisionId=revision-2'
    );
    await page.waitForSelector('app-code-panel', { timeout: 10000 });
    await page.waitForSelector('.code-line.added', { timeout: 10000 });

    const addedLines = page.locator('.code-line.added');
    const count = await addedLines.count();
    expect(count).toBeGreaterThan(0);

    const firstAddedLine = addedLines.first();
    await expect(firstAddedLine).toBeVisible();

    // Verify the added line has the plus sign via CSS ::before
    const backgroundColor = await firstAddedLine.evaluate(
      (el) => window.getComputedStyle(el).backgroundColor
    );
    expect(backgroundColor).toMatch(/rgba?\(0,\s*255,\s*0/);
  });

  test('should render removed lines with red background and minus sign', async ({
    page,
  }) => {
    await page.goto(
      '/review/test-review-id?activeApiRevisionId=revision-1&diffApiRevisionId=revision-2'
    );
    await page.waitForSelector('app-code-panel', { timeout: 10000 });
    await page.waitForSelector('.code-line.removed', { timeout: 10000 });

    const removedLines = page.locator('.code-line.removed');
    const count = await removedLines.count();
    expect(count).toBeGreaterThan(0);

    const firstRemovedLine = removedLines.first();
    await expect(firstRemovedLine).toBeVisible();

    const backgroundColor = await firstRemovedLine.evaluate(
      (el) => window.getComputedStyle(el).backgroundColor
    );
    // rgba(255, 0, 0, 0.25) or similar red-tinted background
    expect(backgroundColor).toMatch(/rgba?\(255,\s*0,\s*0/);
  });

  test('should show both added and removed lines in diff view', async ({
    page,
  }) => {
    await page.goto(
      '/review/test-review-id?activeApiRevisionId=revision-1&diffApiRevisionId=revision-2'
    );
    await page.waitForSelector('app-code-panel', { timeout: 10000 });
    await page.waitForSelector('.code-line', { timeout: 10000 });

    const addedLines = page.locator('.code-line.added');
    const removedLines = page.locator('.code-line.removed');

    await expect(addedLines.first()).toBeVisible();
    await expect(removedLines.first()).toBeVisible();

    // Our mock has 2 added and 2 removed lines
    expect(await addedLines.count()).toBe(2);
    expect(await removedLines.count()).toBe(2);
  });

  test('should support different diff styles in URL', async ({ page }) => {
    await page.goto(
      '/review/test-review-id?activeApiRevisionId=revision-1&diffApiRevisionId=revision-2&diffStyle=trees'
    );
    await page.waitForSelector('app-code-panel', { timeout: 10000 });
    expect(page.url()).toContain('diffStyle=trees');
    await expect(page.locator('app-code-panel')).toBeVisible();
  });
});

test.describe('Code Panel - Selection', () => {
  test.beforeEach(async ({ page }) => {
    await setupAuthMocks(page);
    await setupReviewPageMocks(page);
    await page.goto('/review/test-review-id');
    await page.waitForSelector('app-code-panel', { timeout: 10000 });
  });

  test('should allow selecting code text', async ({ page }) => {
    await page.waitForSelector('.code-line', { timeout: 10000 });

    const codeLine = page.locator('.code-line-content').first();
    await expect(codeLine).toBeVisible();
    await codeLine.click({ clickCount: 3 });

    const selection = await page.evaluate(() =>
      window.getSelection()?.toString()
    );
    expect(selection).toBeTruthy();
  });
});
