import { test, expect } from '@playwright/test';
import { ReviewPage } from '../../page-objects/review.page';
import { setupAuthMocks, setupReviewPageMocks } from '../../mocks/api-handlers';
import {
  mockCodePanelDataWithRelatedComments,
  mockCommentsWithRelatedComments,
} from '../../fixtures';

test.describe('Related Comments - Display', () => {
  test('should display "X related" button when comment has correlationId with matching comments', async ({
    page,
  }) => {
    await setupAuthMocks(page);
    await setupReviewPageMocks(page, {
      codePanelData: mockCodePanelDataWithRelatedComments,
      comments: mockCommentsWithRelatedComments,
    });

    const reviewPage = new ReviewPage(page);
    await reviewPage.goto('test-review-id');
    await reviewPage.waitForReviewLoaded();

    await page.waitForSelector('app-comment-thread', { timeout: 10000 });
    const relatedButton = page.locator('.related-comments-btn');
    await expect(relatedButton.first()).toBeVisible({ timeout: 5000 });
  });

  test('should display correct count in related button', async ({ page }) => {
    await setupAuthMocks(page);
    await setupReviewPageMocks(page, {
      codePanelData: mockCodePanelDataWithRelatedComments,
      comments: mockCommentsWithRelatedComments,
    });

    const reviewPage = new ReviewPage(page);
    await reviewPage.goto('test-review-id');
    await reviewPage.waitForReviewLoaded();

    await page.waitForSelector('app-comment-thread', { timeout: 10000 });

    const relatedButton = page.locator('.related-comments-btn').first();
    await expect(relatedButton).toBeVisible({ timeout: 5000 });

    const buttonText = await relatedButton.textContent();
    expect(buttonText).toContain('1 related');
  });
});

test.describe('Related Comments - Dialog', () => {
  test('should open related comments dialog when clicking "View related" button', async ({
    page,
  }) => {
    await setupAuthMocks(page);
    await setupReviewPageMocks(page, {
      codePanelData: mockCodePanelDataWithRelatedComments,
      comments: mockCommentsWithRelatedComments,
    });

    const reviewPage = new ReviewPage(page);
    await reviewPage.goto('test-review-id');
    await reviewPage.waitForReviewLoaded();
    await page.waitForSelector('app-comment-thread', { timeout: 10000 });

    const relatedButton = page.locator('.related-comments-btn').first();
    await relatedButton.click();

    const dialog = page.getByRole('dialog', { name: 'View related issues' });
    await expect(dialog).toBeVisible({ timeout: 5000 });
  });

  test('should display all related comments in the dialog', async ({
    page,
  }) => {
    await setupAuthMocks(page);
    await setupReviewPageMocks(page, {
      codePanelData: mockCodePanelDataWithRelatedComments,
      comments: mockCommentsWithRelatedComments,
    });

    const reviewPage = new ReviewPage(page);
    await reviewPage.goto('test-review-id');
    await reviewPage.waitForReviewLoaded();
    await page.waitForSelector('app-comment-thread', { timeout: 10000 });

    const relatedButton = page.locator('.related-comments-btn').first();
    await relatedButton.click();

    const dialog = page.getByRole('dialog', { name: 'View related issues' });
    await expect(dialog).toBeVisible({ timeout: 5000 });

    const commentItems = dialog.locator(
      '.comment-item, .related-comments-list .comment'
    );
    const count = await commentItems.count();
    expect(count).toBe(2);
  });

  test('should allow selecting individual comments via checkbox', async ({
    page,
  }) => {
    await setupAuthMocks(page);
    await setupReviewPageMocks(page, {
      codePanelData: mockCodePanelDataWithRelatedComments,
      comments: mockCommentsWithRelatedComments,
    });

    const reviewPage = new ReviewPage(page);
    await reviewPage.goto('test-review-id');
    await reviewPage.waitForReviewLoaded();
    await page.waitForSelector('app-comment-thread', { timeout: 10000 });

    const relatedButton = page.locator('.related-comments-btn').first();
    await relatedButton.click();

    const dialog = page.getByRole('dialog', { name: 'View related issues' });
    await expect(dialog).toBeVisible({ timeout: 5000 });

    const commentCheckboxes = dialog.locator('.comment-item p-checkbox');
    const firstCheckbox = commentCheckboxes.first();
    const checkboxInput = firstCheckbox.locator('input[type="checkbox"]');
    const wasChecked = await checkboxInput.isChecked();

    await firstCheckbox.click();

    const isNowChecked = await expect.poll(async () => await checkboxInput.isChecked(), { timeout: 3000 }).toBe(!wasChecked);
    expect(await checkboxInput.isChecked()).toBe(!wasChecked);
  });

  test('should allow "Select All" to select all comments', async ({ page }) => {
    await setupAuthMocks(page);
    await setupReviewPageMocks(page, {
      codePanelData: mockCodePanelDataWithRelatedComments,
      comments: mockCommentsWithRelatedComments,
    });

    const reviewPage = new ReviewPage(page);
    await reviewPage.goto('test-review-id');
    await reviewPage.waitForReviewLoaded();
    await page.waitForSelector('app-comment-thread', { timeout: 10000 });

    const relatedButton = page.locator('.related-comments-btn').first();
    await relatedButton.click();

    const dialog = page.getByRole('dialog', { name: 'View related issues' });
    await expect(dialog).toBeVisible({ timeout: 5000 });

    const selectAllCheckbox = dialog
      .locator('.select-all-section p-checkbox')
      .first();
    await selectAllCheckbox.click();

    const allCheckboxInputs = dialog.locator(
      '.comment-item p-checkbox input[type="checkbox"]'
    );
    await expect.poll(async () => {
      return await allCheckboxInputs.evaluateAll((inputs) =>
        inputs.every((input) => (input as HTMLInputElement).checked)
      );
    }, { timeout: 3000 }).toBe(true);

    const allChecked = await allCheckboxInputs.evaluateAll((inputs) =>
      inputs.every((input) => (input as HTMLInputElement).checked)
    );
    expect(allChecked).toBe(true);
  });

  test('should close dialog when clicking outside (dismissable mask)', async ({
    page,
  }) => {
    await setupAuthMocks(page);
    await setupReviewPageMocks(page, {
      codePanelData: mockCodePanelDataWithRelatedComments,
      comments: mockCommentsWithRelatedComments,
    });

    const reviewPage = new ReviewPage(page);
    await reviewPage.goto('test-review-id');
    await reviewPage.waitForReviewLoaded();
    await page.waitForSelector('app-comment-thread', { timeout: 10000 });

    const relatedButton = page.locator('.related-comments-btn').first();
    await relatedButton.click();

    const dialog = page.getByRole('dialog', { name: 'View related issues' });
    await expect(dialog).toBeVisible({ timeout: 5000 });

    const mask = page.locator('.p-dialog-mask').first();
    await mask.click({ position: { x: 10, y: 10 } });

    await expect(dialog).not.toBeVisible({ timeout: 3000 });
  });
});

test.describe('Related Comments - Batch Operations Workflows', () => {
  test('should complete full batch resolve workflow: select comments -> resolve -> verify API', async ({
    page,
  }) => {
    let batchRequest: { url: string; body: any } | null = null;
    const resolveCalls: { url: string; elementId: string }[] = [];

    await setupAuthMocks(page);
    await setupReviewPageMocks(page, {
      codePanelData: mockCodePanelDataWithRelatedComments,
      comments: mockCommentsWithRelatedComments,
    });

    // Intercept batch operation API - correct endpoint pattern
    await page.route(
      '**/api/comments/**/commentsBatchOperation',
      async (route) => {
        const postData = route.request().postData();
        batchRequest = {
          url: route.request().url(),
          body: postData ? JSON.parse(postData) : null,
        };

        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([]), // Returns array of created comments
        });
      }
    );

    await page.route('**/api/comments/**/resolveComments**', async (route) => {
      const url = route.request().url();
      const urlObj = new URL(url);
      const elementId = urlObj.searchParams.get('elementId') || '';
      resolveCalls.push({ url, elementId });

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({}),
      });

      await page.evaluate((elemId) => {
        window.dispatchEvent(
          new CustomEvent('mock-signalr-comment-resolved', {
            detail: { elementId: elemId },
          })
        );
      }, elementId);
    });

    const reviewPage = new ReviewPage(page);
    await reviewPage.goto('test-review-id');
    await reviewPage.waitForReviewLoaded();

    await page.waitForSelector('app-comment-thread', { timeout: 10000 });

    // Open related comments dialog
    const relatedButton = page.locator('.related-comments-btn').first();
    await expect(relatedButton).toBeVisible({ timeout: 5000 });
    await relatedButton.click();

    const dialog = page.getByRole('dialog', { name: 'View related issues' });
    await expect(dialog).toBeVisible({ timeout: 5000 });

    // Select all comments
    const selectAll = dialog.locator('.select-all-section p-checkbox').first();
    await expect(selectAll).toBeVisible({ timeout: 3000 });
    await selectAll.click();

    // Choose "Resolve" disposition from dropdown
    const dispositionDropdown = dialog.locator('p-select').first();
    await expect(dispositionDropdown).toBeVisible({ timeout: 3000 });
    await dispositionDropdown.click();

    const resolveOption = page
      .locator('.p-select-option')
      .filter({ hasText: /resolve/i });
    await expect(resolveOption).toBeVisible({ timeout: 3000 });
    await resolveOption.click();

    // Click "Apply Changes" button
    const submitButton = dialog
      .locator('button')
      .filter({ hasText: /apply changes/i })
      .first();
    await expect(submitButton).toBeVisible({ timeout: 3000 });
    await submitButton.click();
    await expect(dialog).not.toBeVisible({ timeout: 3000 });

    expect(batchRequest).not.toBeNull();
    expect(batchRequest!.url).toContain('commentsBatchOperation');
    expect(batchRequest!.body).toBeDefined();
    expect(batchRequest!.body.disposition).toBe('resolve');
    expect(batchRequest!.body.commentIds).toBeDefined();
    expect(batchRequest!.body.commentIds.length).toBe(2);
    await expect(dialog).not.toBeVisible({ timeout: 3000 });
  });

  test('should complete full batch upvote workflow: select comments -> upvote -> verify API', async ({
    page,
  }) => {
    let batchRequest: { url: string; body: any } | null = null;

    await setupAuthMocks(page);
    await setupReviewPageMocks(page, {
      codePanelData: mockCodePanelDataWithRelatedComments,
      comments: mockCommentsWithRelatedComments,
    });

    // Intercept batch operation API
    await page.route(
      '**/api/comments/**/commentsBatchOperation',
      async (route) => {
        const postData = route.request().postData();
        batchRequest = {
          url: route.request().url(),
          body: postData ? JSON.parse(postData) : null,
        };

        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([]),
        });
      }
    );

    const reviewPage = new ReviewPage(page);
    await reviewPage.goto('test-review-id');
    await reviewPage.waitForReviewLoaded();

    await page.waitForSelector('app-comment-thread', { timeout: 10000 });

    // Open dialog
    const relatedButton = page.locator('.related-comments-btn').first();
    await expect(relatedButton).toBeVisible({ timeout: 5000 });
    await relatedButton.click();
    const dialog = page.getByRole('dialog', { name: 'View related issues' });
    await expect(dialog).toBeVisible({ timeout: 5000 });

    // Select all
    const selectAll = dialog.locator('.select-all-section p-checkbox').first();
    await expect(selectAll).toBeVisible({ timeout: 3000 });
    await selectAll.click();

    // Click upvote button (thumbs up icon)
    const upvoteButton = dialog
      .locator('.vote-btn')
      .filter({ has: page.locator('.bi-hand-thumbs-up') })
      .first();
    await expect(upvoteButton).toBeVisible({ timeout: 3000 });
    await upvoteButton.click();

    // Click "Apply Changes" button
    const submitButton = dialog
      .locator('button')
      .filter({ hasText: /apply changes/i })
      .first();
    await expect(submitButton).toBeVisible({ timeout: 3000 });
    await submitButton.click();
    await expect.poll(() => batchRequest, { timeout: 5000 }).not.toBeNull();

    expect(batchRequest).not.toBeNull();
    expect(batchRequest!.url).toContain('commentsBatchOperation');
    expect(batchRequest!.body).toBeDefined();
    expect(batchRequest!.body.vote).toBe('up');
    expect(batchRequest!.body.commentIds).toBeDefined();
    expect(batchRequest!.body.commentIds.length).toBe(2);
  });

  test('should complete full batch reply workflow: select comments -> type reply -> submit -> verify API -> verify UI', async ({
    page,
  }) => {
    let batchRequest: { url: string; body: any } | null = null;
    const testReplyText =
      'Test reply - should appear instantly after submission';

    await setupAuthMocks(page);
    await setupReviewPageMocks(page, {
      codePanelData: mockCodePanelDataWithRelatedComments,
      comments: mockCommentsWithRelatedComments,
    });

    // Mock the batch operation API to return created reply comments with proper structure
    await page.route(
      '**/api/comments/**/commentsBatchOperation',
      async (route) => {
        const postData = route.request().postData();
        batchRequest = {
          url: route.request().url(),
          body: postData ? JSON.parse(postData) : null,
        };

        const createdComments = [
          {
            id: `reply-${Date.now()}-0`,
            reviewId: 'test-review-id',
            apiRevisionId: 'revision-1',
            elementId: 'Azure.Storage.Blobs.BlobClient',
            sectionClass: '',
            commentText: batchRequest?.body?.commentReply || testReplyText,
            createdBy: 'testuser',
            createdOn: new Date().toISOString(),
            lastEditedOn: null,
            isResolved: false,
            upvotes: [],
            downvotes: [],
            taggedUsers: [],
            commentType: 1,
            resolutionLocked: false,
            crossLanguageId: null,
            isDeleted: false,
            isInEditMode: false,
          },
          {
            id: `reply-${Date.now()}-1`,
            reviewId: 'test-review-id',
            apiRevisionId: 'revision-1',
            elementId: 'Azure.Storage.Blobs.BlobClient.Upload',
            sectionClass: '',
            commentText: batchRequest?.body?.commentReply || testReplyText,
            createdBy: 'testuser',
            createdOn: new Date().toISOString(),
            lastEditedOn: null,
            isResolved: false,
            upvotes: [],
            downvotes: [],
            taggedUsers: [],
            commentType: 1,
            resolutionLocked: false,
            crossLanguageId: null,
            isDeleted: false,
            isInEditMode: false,
          },
        ];

        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(createdComments),
        });
      }
    );

    const reviewPage = new ReviewPage(page);
    await reviewPage.goto('test-review-id');
    await reviewPage.waitForReviewLoaded();
    await page.waitForSelector('app-comment-thread', { timeout: 10000 });

    // Capture initial state - count existing comments in the page
    const initialCommentContents = await page
      .locator('.rendered-comment-content')
      .allTextContents();
    const initialCommentCount = initialCommentContents.length;

    // Open the related comments dialog
    const relatedButton = page.locator('.related-comments-btn').first();
    await expect(relatedButton).toBeVisible({ timeout: 5000 });
    await relatedButton.click();

    const dialog = page.getByRole('dialog', { name: 'View related issues' });
    await expect(dialog).toBeVisible({ timeout: 5000 });

    // Select all comments to enable batch operations
    const selectAll = dialog.locator('.select-all-section p-checkbox').first();
    await expect(selectAll).toBeVisible({ timeout: 3000 });
    await selectAll.click();

    // Type a reply in the resolution comment textarea
    const replyTextarea = dialog.locator('.resolution-comment-textarea');
    await expect(replyTextarea).toBeVisible({ timeout: 3000 });
    await replyTextarea.fill(testReplyText);

    // Click "Apply Changes" button
    const submitButton = dialog
      .locator('button')
      .filter({ hasText: /apply changes/i })
      .first();
    await expect(submitButton).toBeVisible({ timeout: 3000 });
    await submitButton.click();

    // Wait for the dialog to close (indicates successful submission)
    await expect(dialog).not.toBeVisible({ timeout: 5000 });

    // Verify the batch operation API was called with the reply text
    expect(batchRequest).not.toBeNull();
    expect(batchRequest!.url).toContain('commentsBatchOperation');
    expect(batchRequest!.body).toBeDefined();
    expect(batchRequest!.body.commentReply).toBe(testReplyText);
    expect(batchRequest!.body.commentIds).toBeDefined();
    expect(batchRequest!.body.commentIds.length).toBeGreaterThan(0);

    // Visual validation: Check that the new replies appear in the UI
    const replyAppeared = await page
      .waitForFunction(
        (replyText) => {
          const contents = Array.from(
            document.querySelectorAll('.rendered-comment-content')
          ).map((el) => el.textContent || '');
          return contents.some((content) => content.includes(replyText));
        },
        testReplyText,
        { timeout: 5000 }
      )
      .then(() => true)
      .catch(() => false);
    expect(replyAppeared).toBe(true);

    const allCommentContents = await page
      .locator('.rendered-comment-content')
      .allTextContents();
    const replyCount = allCommentContents.filter((content) =>
      content.includes(testReplyText)
    ).length;
    expect(replyCount).toBe(2);
    await expect(page.locator('app-comment-thread').first()).toBeVisible();
  });

  test('should complete full batch delete workflow: select comments -> delete -> provide reason -> submit', async ({
    page,
  }) => {
    let batchRequest: { url: string; body: any } | null = null;
    const deleteReason = 'These comments are incorrect and need to be removed.';

    await setupAuthMocks(page);
    await setupReviewPageMocks(page, {
      codePanelData: mockCodePanelDataWithRelatedComments,
      comments: mockCommentsWithRelatedComments,
    });

    // Intercept batch operation API
    await page.route(
      '**/api/comments/**/commentsBatchOperation',
      async (route) => {
        const postData = route.request().postData();
        batchRequest = {
          url: route.request().url(),
          body: postData ? JSON.parse(postData) : null,
        };

        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([]),
        });
      }
    );

    const reviewPage = new ReviewPage(page);
    await reviewPage.goto('test-review-id');
    await reviewPage.waitForReviewLoaded();

    await page.waitForSelector('app-comment-thread', { timeout: 10000 });

    // Open dialog
    const relatedButton = page.locator('.related-comments-btn').first();
    await expect(relatedButton).toBeVisible({ timeout: 5000 });
    await relatedButton.click();
    const dialog = page.getByRole('dialog', { name: 'View related issues' });
    await expect(dialog).toBeVisible({ timeout: 5000 });

    // Select all
    const selectAll = dialog.locator('.select-all-section p-checkbox').first();
    await expect(selectAll).toBeVisible({ timeout: 3000 });
    await selectAll.click();

    // Choose "Delete" disposition from first dropdown (disposition dropdown)
    const dispositionDropdown = dialog
      .locator('.bulk-actions-section p-select')
      .first();
    await expect(dispositionDropdown).toBeVisible({ timeout: 3000 });
    await dispositionDropdown.click();

    const deleteOption = page
      .locator('.p-select-option')
      .filter({ hasText: /delete/i });
    await expect(deleteOption).toBeVisible({ timeout: 3000 });
    await deleteOption.click();

    // Provide deletion reason (required for delete - appears after selecting delete)
    const reasonTextarea = dialog.locator('#deletionReason');
    await expect(reasonTextarea).toBeVisible({ timeout: 3000 });
    await reasonTextarea.fill(deleteReason);

    const submitButton = dialog
      .locator('button')
      .filter({ hasText: /apply changes/i })
      .first();
    await expect(submitButton).toBeVisible({ timeout: 3000 });
    await submitButton.click();
    await expect.poll(() => batchRequest, { timeout: 5000 }).not.toBeNull();

    expect(batchRequest).not.toBeNull();
    expect(batchRequest!.url).toContain('commentsBatchOperation');
    expect(batchRequest!.body).toBeDefined();
    expect(batchRequest!.body.disposition).toBe('delete');
    expect(batchRequest!.body.commentIds).toBeDefined();
    expect(batchRequest!.body.commentIds.length).toBeGreaterThan(0);
  });
});
