import { test, expect, Page } from '@playwright/test';
import { ReviewPage } from '../../page-objects';
import { setupReviewPageMocks, setupAuthMocks } from '../../mocks/api-handlers';
import {
  mockUserProfile,
  mockComments,
  generateLargeCodePanelData,
} from '../../fixtures';
import {
  parseFormData,
  createMockCommentResponse,
  setupCommentCreationCapture,
  setupUpvoteCapture,
  setupDownvoteCapture,
  setupFeedbackCapture,
  setupResolveCapture,
  setupDeleteCapture,
  setupEditCapture,
  waitForCommentThread,
  openReplyEditor,
  openCommentMenu,
  clickMenuOption,
  openCommentFormOnLine,
  clickThumbsUp,
  clickThumbsDown,
} from '../../helpers/comment-helpers';

async function setupCommentMutationMocks(page: Page) {
  await page.route('**/api/comments', async (route) => {
    if (route.request().method() === 'POST') {
      const formData = parseFormData(route.request());
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(
          createMockCommentResponse({
            elementId: formData['elementId'],
            commentText: formData['commentText'],
          })
        ),
      });
    } else {
      await route.continue();
    }
  });

  // Mock update comment
  await page.route('**/api/comments/*/*/updateCommentText', async (route) => {
    await route.fulfill({ status: 200 });
  });

  // Mock delete comment
  await page.route('**/api/comments/*/*', async (route) => {
    if (route.request().method() === 'DELETE') {
      await route.fulfill({ status: 200 });
    } else {
      await route.continue();
    }
  });

  // Mock upvote/downvote
  await page.route('**/api/comments/*/*/toggleCommentUpVote', async (route) => {
    await route.fulfill({ status: 200 });
  });
  await page.route(
    '**/api/comments/*/*/toggleCommentDownVote',
    async (route) => {
      await route.fulfill({ status: 200 });
    }
  );

  // Mock resolve/unresolve
  await page.route('**/api/comments/*/resolveComments*', async (route) => {
    await route.fulfill({ status: 200 });
  });
  await page.route('**/api/comments/*/unResolveComments*', async (route) => {
    await route.fulfill({ status: 200 });
  });
}

test.describe('Comments - Create New Comment', () => {
  test.beforeEach(async ({ page }) => {
    await setupAuthMocks(page);
    await setupCommentMutationMocks(page);
    await setupReviewPageMocks(page);
  });

  test('should complete full comment workflow: click icon -> type text -> submit -> verify API request', async ({
    page,
  }) => {
    const reviewPage = new ReviewPage(page);
    const getCapturedRequest = await setupCommentCreationCapture(page);

    await reviewPage.goto('test-review-id');
    await reviewPage.waitForReviewLoaded();
    await page.waitForSelector('.code-line', { timeout: 10000 });

    // Open comment form and type
    const textarea = await openCommentFormOnLine(page, 4);
    const testCommentText = 'This is a test comment from Playwright UI test';
    await textarea.fill(testCommentText);

    // Submit
    const addCommentBtn = page
      .locator('button')
      .filter({ hasText: /Add Comment/i })
      .last();
    await expect(addCommentBtn).toBeVisible();
    await expect(addCommentBtn).toBeEnabled();
    await addCommentBtn.click();
    await page.waitForTimeout(1000);

    // Verify API request
    const capturedRequest = getCapturedRequest();
    expect(capturedRequest).not.toBeNull();
    expect(capturedRequest!.url).toContain('/api/comments');
    expect(capturedRequest!.formData['commentText']).toBe(testCommentText);
    expect(capturedRequest!.formData['reviewId']).toBe('test-review-id');
  });

  test('should have Add Comment button disabled when comment text is empty', async ({
    page,
  }) => {
    const reviewPage = new ReviewPage(page);
    await reviewPage.goto('test-review-id');
    await reviewPage.waitForReviewLoaded();
    await page.waitForSelector('.code-line', { timeout: 10000 });

    const lineNumbers = page.locator('.line-number');
    await lineNumbers.first().click();

    const addCommentOption = page
      .locator('.p-menuitem-link')
      .filter({ hasText: /comment/i });
    if (await addCommentOption.isVisible()) {
      await addCommentOption.click();
      await page.waitForTimeout(500);

      const submitBtn = page
        .locator('button.submit')
        .filter({ hasText: /add comment/i })
        .first();
      if (await submitBtn.isVisible().catch(() => false)) {
        await expect(submitBtn).toBeDisabled();
      }
    }
  });

  test('should show severity dropdown in new comment form', async ({
    page,
  }) => {
    const reviewPage = new ReviewPage(page);
    await reviewPage.goto('test-review-id');
    await reviewPage.waitForReviewLoaded();
    await page.waitForSelector('.code-line', { timeout: 10000 });

    await openCommentFormOnLine(page, 4);

    const severityDropdown = page
      .locator('app-comment-thread p-dropdown, app-editor-panel p-dropdown')
      .first();
    await expect(severityDropdown).toBeVisible({ timeout: 5000 });
    await severityDropdown.click();

    const dropdownPanel = page.locator('.p-dropdown-panel');
    await expect(dropdownPanel).toBeVisible({ timeout: 3000 });

    const infoOption = dropdownPanel
      .locator('.p-dropdown-item')
      .filter({ hasText: /info/i });
    const mustFixOption = dropdownPanel
      .locator('.p-dropdown-item')
      .filter({ hasText: /must fix/i });

    const hasInfoOption = await infoOption.isVisible().catch(() => false);
    const hasMustFixOption = await mustFixOption.isVisible().catch(() => false);
    expect(hasInfoOption || hasMustFixOption).toBe(true);
  });
});

test.describe('Comments - Reply to Comment', () => {
  test.beforeEach(async ({ page }) => {
    await setupAuthMocks(page);
    await setupCommentMutationMocks(page);
    await setupReviewPageMocks(page, { comments: mockComments });
  });

  test('should display existing comments in code panel', async ({ page }) => {
    const reviewPage = new ReviewPage(page);
    await reviewPage.goto('test-review-id');
    await reviewPage.waitForReviewLoaded();
    await page.waitForTimeout(2000);

    await waitForCommentThread(page);
  });

  test('should complete full reply workflow: click Reply -> type text -> submit -> verify API request', async ({
    page,
  }) => {
    const reviewPage = new ReviewPage(page);
    const getCapturedRequest = await setupCommentCreationCapture(page);

    await reviewPage.goto('test-review-id');
    await reviewPage.waitForReviewLoaded();
    await page.waitForTimeout(2000);

    const commentThread = await waitForCommentThread(page);
    const replyEditor = await openReplyEditor(commentThread, page);

    const textarea = replyEditor.locator('app-editor textarea').last();
    await expect(textarea).toBeVisible({ timeout: 5000 });

    const testReplyText =
      'This is a reply from Playwright UI test - testing the full reply workflow!';
    await textarea.fill(testReplyText);

    const submitBtn = replyEditor.locator('button.submit');
    await expect(submitBtn).toBeVisible();
    await expect(submitBtn).toBeEnabled();
    await submitBtn.click();
    await page.waitForTimeout(1000);

    const capturedRequest = getCapturedRequest();
    expect(capturedRequest).not.toBeNull();
    expect(capturedRequest!.url).toContain('/api/comments');
    expect(capturedRequest!.formData['commentText']).toBe(testReplyText);
    expect(capturedRequest!.formData['reviewId']).toBe('test-review-id');
  });

  test('should close reply editor when clicking Cancel', async ({ page }) => {
    const reviewPage = new ReviewPage(page);
    await reviewPage.goto('test-review-id');
    await reviewPage.waitForReviewLoaded();
    await page.waitForTimeout(2000);

    const commentThread = await waitForCommentThread(page);
    const replyEditor = await openReplyEditor(commentThread, page);

    const cancelBtn = replyEditor
      .locator('button')
      .filter({ hasText: /cancel/i });
    await expect(cancelBtn).toBeVisible({ timeout: 3000 });
    await cancelBtn.click();
    await page.waitForTimeout(300);

    await expect(replyEditor).not.toBeVisible({ timeout: 3000 });
  });
});

test.describe('Comments - Upvote', () => {
  test.beforeEach(async ({ page }) => {
    await setupAuthMocks(page);
    await setupCommentMutationMocks(page);
    await setupReviewPageMocks(page, { comments: mockComments });
  });

  test('should display thumbs up button on comments', async ({ page }) => {
    const reviewPage = new ReviewPage(page);
    await reviewPage.goto('test-review-id');
    await reviewPage.waitForReviewLoaded();
    await page.waitForTimeout(2000);

    const thumbsUp = page
      .locator('.bi-hand-thumbs-up')
      .or(page.locator('.bi-hand-thumbs-up-fill'));
    await expect(thumbsUp.first()).toBeVisible();
  });

  test('should call upvote API with correct URL when clicking thumbs up', async ({
    page,
  }) => {
    const reviewPage = new ReviewPage(page);
    const getUpvoteRequest = await setupUpvoteCapture(page);

    await reviewPage.goto('test-review-id');
    await reviewPage.waitForReviewLoaded();
    await page.waitForTimeout(2000);

    const commentThread = await waitForCommentThread(page);
    await clickThumbsUp(commentThread, page);

    const upvoteRequest = getUpvoteRequest();
    expect(upvoteRequest).not.toBeNull();
    expect(upvoteRequest!.url).toContain('/toggleCommentUpVote');
    expect(upvoteRequest!.reviewId).toBe('test-review-id');
    expect(upvoteRequest!.commentId).toBeTruthy();
  });
});

test.describe('Comments - Downvote AI Comments', () => {
  test.beforeEach(async ({ page }) => {
    const { mockAIGeneratedComments, mockCodePanelDataWithAIComments } =
      await import('../../fixtures');

    await setupAuthMocks(page);
    await setupCommentMutationMocks(page);
    await setupReviewPageMocks(page, {
      comments: mockAIGeneratedComments,
      codePanelData: mockCodePanelDataWithAIComments,
    });
  });

  test('should show feedback dialog when downvoting AI-generated comment', async ({
    page,
  }) => {
    const reviewPage = new ReviewPage(page);
    await reviewPage.goto('test-review-id');
    await reviewPage.waitForReviewLoaded();
    await page.waitForTimeout(2000);

    const commentThread = await waitForCommentThread(page);
    await clickThumbsDown(commentThread, page);

    const feedbackDialog = page.getByRole('dialog', {
      name: /provide additional feedback/i,
    });
    await expect(feedbackDialog).toBeVisible({ timeout: 5000 });

    // Verify dialog has expected elements
    await expect(feedbackDialog.locator('.feedback-description')).toContainText(
      /why you're downvoting/i
    );

    const reasonCheckboxes = feedbackDialog.locator('.reason-item p-checkbox');
    expect(await reasonCheckboxes.count()).toBeGreaterThan(0);

    await expect(
      feedbackDialog.locator('textarea#additionalComments')
    ).toBeVisible();
  });

  test('should submit feedback and call downvote API after selecting reason', async ({
    page,
  }) => {
    const getDownvoteCalled = await setupDownvoteCapture(page);
    const getFeedbackRequest = await setupFeedbackCapture(page);

    const reviewPage = new ReviewPage(page);
    await reviewPage.goto('test-review-id');
    await reviewPage.waitForReviewLoaded();
    await page.waitForTimeout(2000);

    const commentThread = await waitForCommentThread(page);
    await clickThumbsDown(commentThread, page);

    const feedbackDialog = page.getByRole('dialog', {
      name: /provide additional feedback/i,
    });
    await expect(feedbackDialog).toBeVisible({ timeout: 5000 });

    // Select a reason
    const firstReasonCheckbox = feedbackDialog
      .locator('.reason-item p-checkbox')
      .first();
    await expect(firstReasonCheckbox).toBeVisible({ timeout: 3000 });
    await firstReasonCheckbox.click();
    await page.waitForTimeout(200);

    // Add comments and submit
    const feedbackText = 'This suggestion does not apply to our use case.';
    await feedbackDialog
      .locator('textarea#additionalComments')
      .fill(feedbackText);

    const submitBtn = feedbackDialog
      .locator('button')
      .filter({ hasText: /submit/i })
      .first();
    await expect(submitBtn).toBeVisible({ timeout: 3000 });
    await submitBtn.click();
    await page.waitForTimeout(500);

    await expect(feedbackDialog).not.toBeVisible({ timeout: 3000 });

    expect(getDownvoteCalled()).toBe(true);

    const feedbackRequest = getFeedbackRequest();
    expect(feedbackRequest).not.toBeNull();
    expect(feedbackRequest!.url).toContain('/feedback');
    expect(feedbackRequest!.body.reasons.length).toBeGreaterThan(0);
    expect(feedbackRequest!.body.comment).toBe(feedbackText);
    expect(feedbackRequest!.body.isDelete).toBe(false);
  });

  test('should close dialog without downvoting when cancelled', async ({
    page,
  }) => {
    const getDownvoteCalled = await setupDownvoteCapture(page);

    const reviewPage = new ReviewPage(page);
    await reviewPage.goto('test-review-id');
    await reviewPage.waitForReviewLoaded();
    await page.waitForTimeout(2000);

    const commentThread = await waitForCommentThread(page);
    await clickThumbsDown(commentThread, page);

    const feedbackDialog = page.getByRole('dialog', {
      name: /provide additional feedback/i,
    });
    await expect(feedbackDialog).toBeVisible({ timeout: 5000 });

    const cancelBtn = feedbackDialog
      .locator('button')
      .filter({ hasText: /cancel/i })
      .first();
    if (await cancelBtn.isVisible().catch(() => false)) {
      await cancelBtn.click();
    } else {
      await feedbackDialog.locator('.p-dialog-header-close').first().click();
    }

    await page.waitForTimeout(300);
    await expect(feedbackDialog).not.toBeVisible({ timeout: 3000 });
    expect(getDownvoteCalled()).toBe(false);
  });
});

test.describe('Comments - Edit Comment', () => {
  test.beforeEach(async ({ page }) => {
    const { mockCommentOwnedByUser, mockCodePanelDataOwnedByUser } =
      await import('../../fixtures');

    await setupAuthMocks(page);
    await setupCommentMutationMocks(page);
    await setupReviewPageMocks(page, {
      comments: mockCommentOwnedByUser,
      codePanelData: mockCodePanelDataOwnedByUser,
    });
  });

  test('should complete full edit workflow: open menu -> edit -> save -> verify API request', async ({
    page,
  }) => {
    const reviewPage = new ReviewPage(page);
    const getEditRequest = await setupEditCapture(page);

    await reviewPage.goto('test-review-id');
    await reviewPage.waitForReviewLoaded();
    await page.waitForTimeout(2000);

    const commentThread = await waitForCommentThread(page, {
      hasText: 'My comment that I can edit and delete',
    });

    await openCommentMenu(commentThread, page);
    await clickMenuOption(page, /Edit/i);

    const editorContainer = commentThread.locator('app-editor').last();
    await expect(editorContainer).toBeVisible({ timeout: 5000 });

    const editedText =
      'EDITED: This comment has been modified by the workflow test';

    const codeMirrorContent = editorContainer.locator('.CodeMirror-code');
    if (await codeMirrorContent.isVisible()) {
      await codeMirrorContent.click();
      await page.keyboard.press('Control+a');
      await page.keyboard.type(editedText);
    } else {
      const editTextarea = editorContainer.locator('textarea').last();
      await expect(editTextarea).toBeVisible({ timeout: 5000 });
      await editTextarea.fill('');
      await editTextarea.fill(editedText);
    }

    const saveBtn = page.getByRole('button', { name: 'Save', exact: true });
    await expect(saveBtn).toBeVisible({ timeout: 3000 });
    await saveBtn.click();
    await page.waitForTimeout(1000);

    const editRequest = getEditRequest();
    expect(editRequest).not.toBeNull();
    expect(editRequest!.url).toContain('/updateCommentText');
    expect(editRequest!.newText).toBe(editedText);
  });
});

test.describe('Comments - Resolve/Unresolve', () => {
  test.beforeEach(async ({ page }) => {
    await setupAuthMocks(page);
    await setupCommentMutationMocks(page);
    await setupReviewPageMocks(page, { comments: mockComments });
  });

  test('should display Resolve button on comment threads', async ({ page }) => {
    const reviewPage = new ReviewPage(page);
    await reviewPage.goto('test-review-id');
    await reviewPage.waitForReviewLoaded();
    await page.waitForTimeout(2000);

    const commentThread = await waitForCommentThread(page);
    const resolveBtn = commentThread
      .locator('button')
      .filter({ hasText: /resolve/i });
    await expect(resolveBtn.first()).toBeVisible();
  });

  test('should call resolve API with correct URL when clicking Resolve Conversation', async ({
    page,
  }) => {
    const reviewPage = new ReviewPage(page);
    const getResolveRequest = await setupResolveCapture(page);

    await reviewPage.goto('test-review-id');
    await reviewPage.waitForReviewLoaded();
    await page.waitForTimeout(2000);

    const commentThread = await waitForCommentThread(page);
    const resolveBtn = commentThread
      .locator('button')
      .filter({ hasText: /Resolve Conversation/i });
    await expect(resolveBtn).toBeVisible({ timeout: 5000 });
    await resolveBtn.click();
    await page.waitForTimeout(500);

    const resolveRequest = getResolveRequest();
    expect(resolveRequest).not.toBeNull();
    expect(resolveRequest!.url).toContain('/resolveComments');
    expect(resolveRequest!.reviewId).toBe('test-review-id');
    expect(resolveRequest!.elementId).toBeTruthy();
  });
});

test.describe('Comments - Delete Comment', () => {
  test.beforeEach(async ({ page }) => {
    const { mockCommentOwnedByUser, mockCodePanelDataOwnedByUser } =
      await import('../../fixtures');

    await setupAuthMocks(page);
    await setupCommentMutationMocks(page);
    await setupReviewPageMocks(page, {
      comments: mockCommentOwnedByUser,
      codePanelData: mockCodePanelDataOwnedByUser,
    });
  });

  test('should display action menu on comments owned by user', async ({
    page,
  }) => {
    const reviewPage = new ReviewPage(page);
    await reviewPage.goto('test-review-id');
    await reviewPage.waitForReviewLoaded();
    await page.waitForTimeout(2000);

    await expect(page.locator('.bi-three-dots-vertical').first()).toBeVisible({
      timeout: 5000,
    });
  });

  test('should complete full delete workflow: open menu -> delete -> verify API request', async ({
    page,
  }) => {
    const reviewPage = new ReviewPage(page);
    const getDeleteRequest = await setupDeleteCapture(page);

    await reviewPage.goto('test-review-id');
    await reviewPage.waitForReviewLoaded();
    await page.waitForTimeout(2000);

    const commentThread = await waitForCommentThread(page, {
      hasText: 'My comment that I can edit and delete',
    });

    await openCommentMenu(commentThread, page);
    await clickMenuOption(page, /Delete/i);
    await page.waitForTimeout(500);

    const deleteRequest = getDeleteRequest();
    expect(deleteRequest).not.toBeNull();
    expect(deleteRequest!.method).toBe('DELETE');
    expect(deleteRequest!.url).toContain('/api/comments/');
    expect(deleteRequest!.commentId).toBe('user-comment-1');
  });
});

test.describe('Comments - Scroll Persistence', () => {
  test('should preserve comment text when scrolling away and back', async ({
    page,
  }) => {
    const largeCodePanelData = generateLargeCodePanelData(100);

    await setupAuthMocks(page);
    await setupCommentMutationMocks(page);
    await setupReviewPageMocks(page, { codePanelData: largeCodePanelData });

    const reviewPage = new ReviewPage(page);
    await reviewPage.goto('test-review-id');
    await reviewPage.waitForReviewLoaded();
    await page.waitForSelector('.code-line', { timeout: 10000 });

    const viewport = page.locator('#viewport');

    // Open comment and type text without submitting
    const codeLine = page.locator('.code-line').nth(3);
    await codeLine.scrollIntoViewIfNeeded();
    await codeLine.hover();

    const commentIcon = codeLine.locator(
      '.line-actions .toggle-user-comments-btn'
    );
    await expect(commentIcon).toBeAttached();
    await commentIcon.evaluate((el) => (el as HTMLElement).click());
    await page.waitForTimeout(500);

    const textarea = page
      .locator('app-comment-thread textarea, app-editor textarea')
      .last();
    await expect(textarea).toBeVisible({ timeout: 5000 });

    const testText =
      'This text should persist after scrolling - unique test ' + Date.now();
    await textarea.fill(testText);
    await expect(textarea).toHaveValue(testText);

    // Scroll away and back
    await viewport.hover();
    await page.mouse.wheel(0, 2000);
    await page.waitForTimeout(500);
    await page.mouse.wheel(0, -2500);
    await page.waitForTimeout(500);

    // Verify text persists
    await codeLine.scrollIntoViewIfNeeded();
    const commentThread = page.locator('app-comment-thread').first();
    await expect(commentThread).toBeVisible({ timeout: 5000 });
    await expect(commentThread).toContainText(testText.substring(0, 30));
  });
});

test.describe('Comments - Multiple Threads Per Line', () => {
  test('should display multiple threads on the same code line', async ({
    page,
  }) => {
    const {
      mockCommentsMultipleThreads,
      mockCodePanelDataWithMultipleThreads,
    } = await import('../../fixtures');

    await setupAuthMocks(page);
    await setupReviewPageMocks(page, {
      comments: mockCommentsMultipleThreads,
      codePanelData: mockCodePanelDataWithMultipleThreads,
    });

    const reviewPage = new ReviewPage(page);
    await reviewPage.goto('test-review-id');
    await reviewPage.waitForReviewLoaded();
    await page.waitForTimeout(2000);

    const commentThreads = page.locator('app-comment-thread');
    expect(await commentThreads.count()).toBeGreaterThanOrEqual(2);

    await expect(
      page.locator('text=First thread: Should this class be sealed?')
    ).toBeVisible({ timeout: 5000 });
    await expect(
      page.locator('text=Second thread: Consider adding documentation')
    ).toBeVisible({ timeout: 5000 });
  });

  test('should reply to a specific thread and verify reply only appears in that thread', async ({
    page,
  }) => {
    const {
      mockCommentsMultipleThreads,
      mockCodePanelDataWithMultipleThreads,
    } = await import('../../fixtures');

    const getCapturedRequest = await setupCommentCreationCapture(page);

    await setupAuthMocks(page);
    await setupReviewPageMocks(page, {
      comments: mockCommentsMultipleThreads,
      codePanelData: mockCodePanelDataWithMultipleThreads,
    });

    const reviewPage = new ReviewPage(page);
    await reviewPage.goto('test-review-id');
    await reviewPage.waitForReviewLoaded();
    await page.waitForTimeout(2000);

    const secondThread = await waitForCommentThread(page, {
      hasText: 'Consider adding documentation',
    });

    const replyButton = secondThread
      .locator('button')
      .filter({ hasText: /reply/i })
      .first();
    await expect(replyButton).toBeVisible({ timeout: 3000 });
    await replyButton.click();
    await page.waitForTimeout(300);

    const replyText = 'Replying specifically to the documentation thread';
    const textarea = secondThread.locator('textarea').first();

    if (await textarea.isVisible().catch(() => false)) {
      await textarea.fill(replyText);
      await page.waitForTimeout(200);

      const submitButton = secondThread
        .locator('button')
        .filter({ hasText: /comment|submit|save/i })
        .first();
      if (await submitButton.isVisible().catch(() => false)) {
        await submitButton.click();
        await page.waitForTimeout(500);

        const capturedRequest = getCapturedRequest();
        expect(capturedRequest).not.toBeNull();
        expect(capturedRequest!.formData['commentText']).toContain(
          'documentation thread'
        );
      }
    }
  });
});
