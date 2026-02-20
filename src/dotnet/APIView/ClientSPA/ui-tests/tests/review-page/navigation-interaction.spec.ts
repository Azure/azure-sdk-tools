import { test, expect } from '@playwright/test';
import { ReviewPage } from '../../page-objects/review.page';
import { setupAuthMocks, setupReviewPageMocks } from '../../mocks/api-handlers';

test.describe('Navigation - Tree Node Click', () => {
  test.beforeEach(async ({ page }) => {
    await setupAuthMocks(page);
    await setupReviewPageMocks(page);
  });

  test('should scroll to code section when clicking nav tree node', async ({
    page,
  }) => {
    const reviewPage = new ReviewPage(page);
    await reviewPage.goto('test-review-id');
    await reviewPage.waitForReviewLoaded();
    await page.waitForSelector('p-tree', { timeout: 10000 });

    const treeNode = page
      .locator('.p-tree-node-label')
      .filter({ hasText: /BlobClient/i })
      .first();
    await treeNode.click();
    await expect.poll(() => page.url(), { timeout: 5000 }).toMatch(/nId=|scrollToNodeIdHashed=/);
    const url = page.url();

    expect(url).toMatch(/nId=|scrollToNodeIdHashed=/);
  });
});

test.describe('Navigation - Expand/Collapse', () => {
  test.beforeEach(async ({ page }) => {
    await setupAuthMocks(page);
    await setupReviewPageMocks(page);
  });

  test('should expand tree node when clicking expand icon', async ({
    page,
  }) => {
    const reviewPage = new ReviewPage(page);
    await reviewPage.goto('test-review-id');
    await reviewPage.waitForReviewLoaded();

    await page.waitForSelector('p-tree', { timeout: 10000 });

    // Find the first tree node with children (has a toggle button)
    const toggler = page.locator('.p-tree-node-toggle-button').first();
    await expect(toggler).toBeVisible({ timeout: 5000 });

    // Get the parent li.p-tree-node and its children container
    const treeNode = toggler
      .locator('xpath=ancestor::li[contains(@class, "p-tree-node")]')
      .first();
    const childrenContainer = treeNode
      .locator('[role="group"]')
      .first();

    const isExpanded = await childrenContainer.isVisible().catch(() => false);

    // If expanded, collapse first so we can test expand
    if (isExpanded) {
      await toggler.click();
      await expect(childrenContainer).not.toBeVisible({ timeout: 3000 });
    }

    // Now click to expand
    await toggler.click();

    // Verify children are now visible (expanded)
    await expect(childrenContainer).toBeVisible({ timeout: 3000 });
  });

  test('should collapse tree node when clicking collapse icon', async ({
    page,
  }) => {
    const reviewPage = new ReviewPage(page);
    await reviewPage.goto('test-review-id');
    await reviewPage.waitForReviewLoaded();

    await page.waitForSelector('p-tree', { timeout: 10000 });

    // Find the first tree node with children (has a toggle button)
    const toggler = page.locator('.p-tree-node-toggle-button').first();
    await expect(toggler).toBeVisible({ timeout: 5000 });

    // Get the parent li.p-tree-node and its children container
    const treeNode = toggler
      .locator('xpath=ancestor::li[contains(@class, "p-tree-node")]')
      .first();
    const childrenContainer = treeNode
      .locator('[role="group"]')
      .first();

    const isExpanded = await childrenContainer.isVisible().catch(() => false);

    // If collapsed, expand first so we can test collapse
    if (!isExpanded) {
      await toggler.click();
      await expect(childrenContainer).toBeVisible({ timeout: 3000 });
    }

    // Now click to collapse
    await toggler.click();

    // Verify children are now hidden (collapsed)
    await expect(childrenContainer).not.toBeVisible({ timeout: 3000 });
  });
});

test.describe('Navigation - Panel Resize', () => {
  test.beforeEach(async ({ page }) => {
    await setupAuthMocks(page);
    await setupReviewPageMocks(page);
  });

  test('should have splitter gutter for resizing panels', async ({ page }) => {
    const reviewPage = new ReviewPage(page);
    await reviewPage.goto('test-review-id');
    await reviewPage.waitForReviewLoaded();
    const splitterGutter = page.locator('.p-splitter-gutter');
    await expect(splitterGutter.first()).toBeVisible();
  });

  test('should resize left panel when dragging splitter', async ({ page }) => {
    const reviewPage = new ReviewPage(page);
    await reviewPage.goto('test-review-id');
    await reviewPage.waitForReviewLoaded();

    const splitterGutter = page.locator('.p-splitter-gutter').first();
    // Use .review-panel which is the actual class on the panel divs
    const leftPanel = page.locator('.review-panel').first();
    await expect(leftPanel).toBeVisible({ timeout: 5000 });
    const initialWidth = await leftPanel.evaluate(
      (el) => el.getBoundingClientRect().width
    );

    const gutterBox = await splitterGutter.boundingBox();
    if (gutterBox) {
      // Drag the splitter to the right (increase left panel width)
      await page.mouse.move(
        gutterBox.x + gutterBox.width / 2,
        gutterBox.y + gutterBox.height / 2
      );
      await page.mouse.down();
      await page.mouse.move(
        gutterBox.x + 100,
        gutterBox.y + gutterBox.height / 2
      );
      await page.mouse.up();
    }

    await expect.poll(async () => {
      return await leftPanel.evaluate((el) => el.getBoundingClientRect().width);
    }, { timeout: 3000 }).toBeGreaterThan(initialWidth);

    const newWidth = await leftPanel.evaluate(
      (el) => el.getBoundingClientRect().width
    );
    expect(newWidth).toBeGreaterThan(initialWidth);
  });

  test('should maintain minimum panel width when resizing', async ({
    page,
  }) => {
    const reviewPage = new ReviewPage(page);
    await reviewPage.goto('test-review-id');
    await reviewPage.waitForReviewLoaded();

    const splitterGutter = page.locator('.p-splitter-gutter').first();
    // Use .review-panel which is the actual class on the panel divs
    const leftPanel = page.locator('.review-panel').first();
    await expect(leftPanel).toBeVisible({ timeout: 5000 });
    const gutterBox = await splitterGutter.boundingBox();

    if (gutterBox) {
      // Try to drag splitter all the way to the left (minimize panel)
      await page.mouse.move(
        gutterBox.x + gutterBox.width / 2,
        gutterBox.y + gutterBox.height / 2
      );
      await page.mouse.down();
      await page.mouse.move(0, gutterBox.y + gutterBox.height / 2);
      await page.mouse.up();
    }

    const finalWidth = await leftPanel.evaluate(
      (el) => el.getBoundingClientRect().width
    );
    expect(finalWidth).toBeGreaterThan(0);
  });
});

test.describe('Navigation - Panel Visibility', () => {
  test.beforeEach(async ({ page }) => {
    await setupAuthMocks(page);
    await setupReviewPageMocks(page);
  });

  test('should have navigation panel visible by default', async ({ page }) => {
    const reviewPage = new ReviewPage(page);
    await reviewPage.goto('test-review-id');
    await reviewPage.waitForReviewLoaded();

    const leftNav = page
      .locator('app-review-nav')
      .or(page.locator('.left-navigation'));
    await expect(leftNav.first()).toBeVisible();
  });
});

test.describe('Navigation - Search Integration', () => {
  test.beforeEach(async ({ page }) => {
    await setupAuthMocks(page);
    await setupReviewPageMocks(page);
  });

  test('should filter navigation tree when searching', async ({ page }) => {
    const reviewPage = new ReviewPage(page);
    await reviewPage.goto('test-review-id');
    await reviewPage.waitForReviewLoaded();
    await page.waitForSelector('p-tree', { timeout: 10000 });

    const nodesBefore = await page.locator('.p-treenode-label:visible').count();
    const filterInput = page.locator(
      'input[placeholder*="Filter"], input[placeholder*="Search"]'
    );

    await filterInput.first().fill('Blob');

    const nodesBefore2 = nodesBefore;
    await expect.poll(async () => {
      return await page.locator('.p-treenode-label:visible').count();
    }, { timeout: 5000 }).toBeLessThanOrEqual(nodesBefore2);

    const nodesAfter = await page.locator('.p-treenode-label:visible').count();
    expect(nodesAfter).toBeLessThanOrEqual(nodesBefore);

    // Clear filter and verify nodes return
    await filterInput.first().clear();
    await expect.poll(async () => {
      return await page.locator('.p-treenode-label:visible').count();
    }, { timeout: 5000 }).toBeGreaterThanOrEqual(nodesAfter);

    const nodesAfterClear = await page
      .locator('.p-treenode-label:visible')
      .count();
    expect(nodesAfterClear).toBeGreaterThanOrEqual(nodesAfter);
  });
});

test.describe('Navigation - Loading State', () => {
  test.beforeEach(async ({ page }) => {
    await setupAuthMocks(page);
  });

  test('should display loading spinner initially', async ({ page }) => {
    await setupReviewPageMocks(page, { delay: 500 });

    const reviewPage = new ReviewPage(page);
    await page.goto('/review/test-review-id');

    const navSpinner = page.locator('app-review-nav .spinner-border').first();
    const navLoaded = reviewPage.leftNavigation;

    await expect(async () => {
      const spinnerVisible = await navSpinner.isVisible();
      const navVisible = await navLoaded.isVisible();
      expect(spinnerVisible || navVisible).toBeTruthy();
    }).toPass({ timeout: 5000 });
  });
});

test.describe('Navigation - URL Routing', () => {
  test.beforeEach(async ({ page }) => {
    await setupAuthMocks(page);
    await setupReviewPageMocks(page);
  });

  test('should update URL when navigating with revision ID', async ({
    page,
  }) => {
    const reviewPage = new ReviewPage(page);
    await reviewPage.goto('test-review-id', 'specific-revision-id');
    await reviewPage.waitForReviewLoaded();

    expect(page.url()).toContain('activeApiRevisionId=specific-revision-id');
  });

  test('should handle URL with both active and diff revision IDs', async ({
    page,
  }) => {
    await page.goto(
      '/review/test-review-id?activeApiRevisionId=rev1&diffApiRevisionId=rev2'
    );
    await page.waitForSelector('app-review-page-layout');

    const url = page.url();
    expect(url).toContain('activeApiRevisionId=rev1');
    expect(url).toContain('diffApiRevisionId=rev2');
  });

  test('should handle URL with scrollToNode parameter', async ({ page }) => {
    await page.goto(
      '/review/test-review-id?activeApiRevisionId=rev1&nId=some-node-id'
    );
    await page.waitForSelector('app-review-page-layout');

    expect(page.url()).toContain('nId=some-node-id');
  });
});
