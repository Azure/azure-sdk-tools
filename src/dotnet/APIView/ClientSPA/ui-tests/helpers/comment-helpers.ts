import { Page, Request, expect, Locator } from '@playwright/test';
import { mockUserProfile } from '../fixtures';

export function parseFormData(request: Request): Record<string, string> {
  const postData = request.postData() || '';
  const formDataEntries: Record<string, string> = {};

  const boundary = request.headers()['content-type']?.split('boundary=')[1];
  if (boundary && postData) {
    const parts = postData.split(`--${boundary}`);
    for (const part of parts) {
      const nameMatch = part.match(/name="([^"]+)"/);
      if (nameMatch) {
        const name = nameMatch[1];
        const valueMatch = part.split('\r\n\r\n')[1];
        if (valueMatch) {
          formDataEntries[name] = valueMatch.replace(/\r\n--$/, '').trim();
        }
      }
    }
  }

  return formDataEntries;
}

export function parseCommentUrlParams(url: string): {
  reviewId: string;
  commentId: string;
} {
  const urlParts = url.split('/');
  return {
    commentId: urlParts[urlParts.length - 2] || urlParts[urlParts.length - 1],
    reviewId: urlParts[urlParts.length - 3] || urlParts[urlParts.length - 2],
  };
}

export interface MockCommentData {
  id?: string;
  reviewId?: string;
  elementId?: string;
  commentText?: string;
  createdBy?: string;
}

export function createMockCommentResponse(data: MockCommentData = {}): object {
  return {
    id: data.id || 'new-comment-' + Date.now(),
    reviewId: data.reviewId || 'test-review-id',
    elementId: data.elementId || 'unknown',
    commentText: data.commentText || '',
    createdOn: new Date().toISOString(),
    createdBy: data.createdBy || mockUserProfile.userName,
    isResolved: false,
    upvotes: [],
    downvotes: [],
    taggedUsers: [],
    commentType: 1,
    resolutionLocked: false,
  };
}

export interface CapturedCommentRequest {
  url: string;
  formData: Record<string, string>;
}

export interface CapturedVoteRequest {
  url: string;
  reviewId: string;
  commentId: string;
}

export interface CapturedResolveRequest {
  url: string;
  reviewId: string;
  elementId: string;
}

export interface CapturedDeleteRequest {
  url: string;
  method: string;
  commentId: string;
}

export interface CapturedEditRequest {
  url: string;
  newText: string;
}

export interface CapturedFeedbackRequest {
  url: string;
  body: { reasons: string[]; comment: string; isDelete: boolean };
}

/**
 * Setup a route handler that captures POST requests to /api/comments
 */
export async function setupCommentCreationCapture(
  page: Page
): Promise<() => CapturedCommentRequest | null> {
  let capturedRequest: CapturedCommentRequest | null = null;

  await page.route('**/api/comments', async (route) => {
    if (route.request().method() === 'POST') {
      const formData = parseFormData(route.request());

      capturedRequest = {
        url: route.request().url(),
        formData,
      };

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

  return () => capturedRequest;
}

/**
 * Setup a route handler that captures upvote requests
 */
export async function setupUpvoteCapture(
  page: Page
): Promise<() => CapturedVoteRequest | null> {
  let capturedRequest: CapturedVoteRequest | null = null;

  await page.route('**/api/comments/*/*/toggleCommentUpVote', async (route) => {
    const url = route.request().url();
    const { reviewId, commentId } = parseCommentUrlParams(url);

    capturedRequest = { url, reviewId, commentId };
    await route.fulfill({ status: 200 });
  });

  return () => capturedRequest;
}

/**
 * Setup a route handler that captures downvote requests
 */
export async function setupDownvoteCapture(page: Page): Promise<() => boolean> {
  let downvoteCalled = false;

  await page.route(
    '**/api/comments/*/*/toggleCommentDownVote',
    async (route) => {
      downvoteCalled = true;
      await route.fulfill({ status: 200 });
    }
  );

  return () => downvoteCalled;
}

/**
 * Setup a route handler that captures feedback requests
 */
export async function setupFeedbackCapture(
  page: Page
): Promise<() => CapturedFeedbackRequest | null> {
  let capturedRequest: CapturedFeedbackRequest | null = null;

  await page.route('**/api/comments/*/*/feedback', async (route) => {
    const postData = route.request().postData();
    if (postData) {
      try {
        capturedRequest = {
          url: route.request().url(),
          body: JSON.parse(postData),
        };
      } catch {
        // Parsing failed
      }
    }
    await route.fulfill({ status: 200 });
  });

  return () => capturedRequest;
}

/**
 * Setup a route handler that captures resolve requests
 */
export async function setupResolveCapture(
  page: Page
): Promise<() => CapturedResolveRequest | null> {
  let capturedRequest: CapturedResolveRequest | null = null;

  await page.route('**/api/comments/*/resolveComments*', async (route) => {
    const url = route.request().url();
    const urlObj = new URL(url);
    const elementId = urlObj.searchParams.get('elementId') || '';
    const urlParts = url.split('/');
    const reviewId = urlParts[urlParts.length - 2];

    capturedRequest = { url, reviewId, elementId };
    await route.fulfill({ status: 200 });
  });

  return () => capturedRequest;
}

/**
 * Setup a route handler that captures delete requests
 */
export async function setupDeleteCapture(
  page: Page
): Promise<() => CapturedDeleteRequest | null> {
  let capturedRequest: CapturedDeleteRequest | null = null;

  await page.route('**/api/comments/*/*', async (route) => {
    if (route.request().method() === 'DELETE') {
      const url = route.request().url();
      const urlParts = url.split('/');
      const commentId = urlParts[urlParts.length - 1];

      capturedRequest = {
        url,
        method: 'DELETE',
        commentId,
      };

      await route.fulfill({ status: 200 });
    } else {
      await route.continue();
    }
  });

  return () => capturedRequest;
}

/**
 * Setup a route handler that captures edit requests
 */
export async function setupEditCapture(
  page: Page
): Promise<() => CapturedEditRequest | null> {
  let capturedRequest: CapturedEditRequest | null = null;

  await page.route('**/api/comments/*/*/updateCommentText', async (route) => {
    const url = route.request().url();
    const formData = parseFormData(route.request());

    capturedRequest = { url, newText: formData['commentText'] || '' };
    await route.fulfill({ status: 200 });
  });

  return () => capturedRequest;
}

export function findCommentThread(
  page: Page,
  options?: { hasText?: string }
): Locator {
  const base = page.locator('app-comment-thread');
  if (options?.hasText) {
    return base.filter({ hasText: options.hasText }).first();
  }
  return base.first();
}

export async function waitForCommentThread(
  page: Page,
  options?: { hasText?: string; timeout?: number }
): Promise<Locator> {
  const thread = findCommentThread(page, options);
  await expect(thread).toBeVisible({ timeout: options?.timeout ?? 5000 });
  return thread;
}

export async function openReplyEditor(
  commentThread: Locator,
  _page: Page
): Promise<Locator> {
  const replyButton = commentThread.locator('.reply-button');
  await expect(replyButton).toBeVisible({ timeout: 5000 });
  await replyButton.click();

  const replyEditorContainer = commentThread.locator('.reply-editor-container');
  await expect(replyEditorContainer).toBeVisible({ timeout: 5000 });

  return replyEditorContainer;
}

export async function openCommentMenu(
  commentThread: Locator,
  page: Page
): Promise<void> {
  const menuButton = commentThread.locator('.bi-three-dots-vertical').first();
  await expect(menuButton).toBeVisible({ timeout: 5000 });
  await menuButton.click();

  await expect(page.locator('.p-menu, .p-tieredmenu').first()).toBeVisible({ timeout: 3000 });
}

export async function clickMenuOption(
  page: Page,
  optionText: RegExp | string
): Promise<void> {
  const option = page
    .locator('.p-menuitem-link')
    .filter({ hasText: optionText });
  await expect(option.first()).toBeVisible({ timeout: 3000 });
  await option.first().click();
}

export async function openCommentFormOnLine(
  page: Page,
  lineIndex: number = 4
): Promise<Locator> {
  const codeLine = page.locator('.code-line').nth(lineIndex);
  await codeLine.scrollIntoViewIfNeeded();

  // Hover over the line-number-container to trigger visibility of add-comment-btn
  const lineNumberContainer = codeLine.locator('.line-number-container');
  await lineNumberContainer.hover();

  const commentBtn = codeLine.locator('.line-actions .add-comment-btn');
  await expect(commentBtn).toBeVisible({ timeout: 5000 });
  await commentBtn.click();

  const textarea = page
    .locator('app-comment-thread textarea, app-editor textarea')
    .last();
  await expect(textarea).toBeVisible({ timeout: 5000 });

  return textarea;
}

export async function clickThumbsUp(
  commentThread: Locator,
  page: Page
): Promise<void> {
  const thumbsUpBtn = commentThread
    .locator('button')
    .filter({
      has: page.locator('.bi-hand-thumbs-up'),
    })
    .first();
  await expect(thumbsUpBtn).toBeVisible({ timeout: 5000 });
  await thumbsUpBtn.click();

  await expect(
    thumbsUpBtn.locator('.bi-hand-thumbs-up-fill, .bi-hand-thumbs-up')
  ).toBeVisible({ timeout: 3000 });
}

export async function clickThumbsDown(
  commentThread: Locator,
  page: Page
): Promise<void> {
  const thumbsDownBtn = commentThread
    .locator('button')
    .filter({
      has: page.locator('.bi-hand-thumbs-down'),
    })
    .first();
  await expect(thumbsDownBtn).toBeVisible({ timeout: 5000 });
  await thumbsDownBtn.click();
}
