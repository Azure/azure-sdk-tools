import { Page, Locator } from '@playwright/test';
import { BasePage } from './base.page';

export class ReviewPage extends BasePage {
  // Main layout elements
  readonly leftNavigation: Locator;
  readonly codePanel: Locator;
  readonly pageOptions: Locator;
  readonly splitter: Locator;

  // Navigation elements
  readonly navTree: Locator;
  readonly navTreeNodes: Locator;

  // Code panel elements
  readonly codeLines: Locator;
  readonly lineNumbers: Locator;
  readonly sectionCarets: Locator;

  // Sidebar elements
  readonly revisionsSidebar: Locator;
  readonly conversationSidebar: Locator;

  // Side menu buttons
  readonly revisionsButton: Locator;
  readonly conversationsButton: Locator;

  // Page options elements
  readonly diffStyleDropdown: Locator;
  readonly showCommentsToggle: Locator;
  readonly showDocumentationToggle: Locator;
  readonly showLineNumbersToggle: Locator;

  // Approval elements
  readonly approveRevisionButton: Locator;
  readonly approveReviewButton: Locator;

  constructor(page: Page) {
    super(page);

    // Main layout
    this.leftNavigation = page.locator('app-review-nav');
    this.codePanel = page.locator('app-code-panel');
    this.pageOptions = page.locator('app-review-page-options');
    this.splitter = page.locator('p-splitter');

    // Navigation
    this.navTree = page.locator('p-tree');
    this.navTreeNodes = page.locator('.p-treenode');

    // Code panel
    this.codeLines = page.locator('.code-line');
    this.lineNumbers = page.locator('.line-number');
    this.sectionCarets = page.locator('.row-fold-caret');

    // Sidebars
    this.revisionsSidebar = page.locator('.revisions-sidebar');
    this.conversationSidebar = page.locator('.conversation-sidebar');

    // Side menu
    this.revisionsButton = page.locator('[tooltip="Revisions"]');
    this.conversationsButton = page.locator('[tooltip="Conversations"]');

    // Page options (these will be refined based on actual implementation)
    this.diffStyleDropdown = page.locator(
      '[data-testid="diff-style-dropdown"]'
    );
    this.showCommentsToggle = page.locator(
      '[data-testid="show-comments-toggle"]'
    );
    this.showDocumentationToggle = page.locator(
      '[data-testid="show-documentation-toggle"]'
    );
    this.showLineNumbersToggle = page.locator(
      '[data-testid="show-line-numbers-toggle"]'
    );

    // Approvals
    this.approveRevisionButton = page.locator(
      '[data-testid="approve-revision-btn"]'
    );
    this.approveReviewButton = page.locator(
      '[data-testid="approve-review-btn"]'
    );
  }

  /**
   * Navigate to a specific review
   */
  async goto(reviewId: string, activeApiRevisionId?: string): Promise<void> {
    let url = `/review/${reviewId}`;
    if (activeApiRevisionId) {
      url += `?activeApiRevisionId=${activeApiRevisionId}`;
    }
    await this.page.goto(url);
    await this.waitForAngularReady();
  }

  async waitForReviewLoaded(): Promise<void> {
    await this.waitForAngularReady();
    await this.codePanel.waitFor({ state: 'visible' });
    await this.codeLines
      .first()
      .waitFor({ state: 'visible', timeout: 10000 })
      .catch(() => {});
  }

  async isLoadFailed(): Promise<boolean> {
    const errorMessage = this.page.locator(
      '[data-testid="load-failed-message"]'
    );
    return await errorMessage.isVisible();
  }

  async getLoadFailedMessage(): Promise<string | null> {
    const errorMessage = this.page.locator(
      '[data-testid="load-failed-message"]'
    );
    if (await errorMessage.isVisible()) {
      return await errorMessage.textContent();
    }
    return null;
  }

  async clickNavNode(nodeName: string): Promise<void> {
    const node = this.navTreeNodes.filter({ hasText: nodeName }).first();
    await node.click();
  }

  async expandNavNode(nodeName: string): Promise<void> {
    const node = this.navTreeNodes.filter({ hasText: nodeName }).first();
    const toggler = node.locator('.p-tree-toggler');
    const isExpanded = await node
      .locator('.p-tree-toggler-icon.pi-chevron-down')
      .isVisible();

    if (!isExpanded) {
      await toggler.click();
    }
  }

  async collapseNavNode(nodeName: string): Promise<void> {
    const node = this.navTreeNodes.filter({ hasText: nodeName }).first();
    const toggler = node.locator('.p-tree-toggler');
    const isExpanded = await node
      .locator('.p-tree-toggler-icon.pi-chevron-down')
      .isVisible();

    if (isExpanded) {
      await toggler.click();
    }
  }

  async toggleLeftNavigation(): Promise<void> {
    const toggleBtn = this.page.locator('[data-testid="toggle-left-nav"]');
    await toggleBtn.click();
  }

  // Code Panel Methods
  async clickCodeLine(lineContent: string): Promise<void> {
    const line = this.codeLines.filter({ hasText: lineContent }).first();
    await line.click();
  }

  async expandCodeSection(sectionIndex: number): Promise<void> {
    const caret = this.sectionCarets.nth(sectionIndex);
    await caret.click();
  }

  async getVisibleCodeLineCount(): Promise<number> {
    return await this.codeLines
      .filter({ has: this.page.locator(':visible') })
      .count();
  }

  async areLineNumbersVisible(): Promise<boolean> {
    const firstLineNumber = this.lineNumbers.first();
    return await firstLineNumber.isVisible();
  }

  // Sidebar Methods
  async openRevisionsSidebar(): Promise<void> {
    await this.revisionsButton.click();
    await this.revisionsSidebar.waitFor({ state: 'visible' });
  }

  async closeRevisionsSidebar(): Promise<void> {
    await this.closeSidebar();
  }

  async openConversationsSidebar(): Promise<void> {
    await this.conversationsButton.click();
    await this.conversationSidebar.waitFor({ state: 'visible' });
  }

  async selectRevision(revisionLabel: string): Promise<void> {
    await this.openRevisionsSidebar();
    const revisionItem = this.revisionsSidebar
      .locator('.revision-item')
      .filter({ hasText: revisionLabel });
    await revisionItem.click();
  }

  // Comment Methods

  async addComment(lineContent: string, commentText: string): Promise<void> {
    // Click on the line to select it
    await this.clickCodeLine(lineContent);

    // Click the add comment button (icon that appears on hover/selection)
    const commentIcon = this.page.locator('.add-comment-icon').first();
    await commentIcon.click();

    // Wait for comment editor
    const commentEditor = this.page.locator('.comment-editor textarea');
    await commentEditor.waitFor({ state: 'visible' });
    await commentEditor.fill(commentText);

    // Submit the comment
    const submitBtn = this.page.locator('.comment-editor .submit-btn');
    await submitBtn.click();

    // Wait for comment to appear
    await this.page
      .locator('.comment-thread')
      .filter({ hasText: commentText })
      .waitFor({ state: 'visible' });
  }

  // Approval Methods
  async approveRevision(): Promise<void> {
    await this.approveRevisionButton.click();
    await this.waitForToast('approved');
  }

  async isRevisionApproved(): Promise<boolean> {
    const approvedBadge = this.page.locator('.approval-badge.approved');
    return await approvedBadge.isVisible();
  }
}
