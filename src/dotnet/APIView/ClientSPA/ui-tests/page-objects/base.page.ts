import { Page, Locator } from '@playwright/test';

export class BasePage {
  readonly page: Page;

  constructor(page: Page) {
    this.page = page;
  }

  async waitForAngularReady(): Promise<void> {
    await this.page.waitForSelector('app-root', { state: 'attached' });

    await this.page
      .waitForSelector('.loading-spinner', { state: 'hidden', timeout: 5000 })
      .catch(() => {});
  }

  async waitForNetworkIdle(): Promise<void> {
    await this.page.waitForLoadState('networkidle');
  }

  getByTestId(testId: string): Locator {
    return this.page.locator(`[data-testid="${testId}"]`);
  }

  async waitForToast(expectedText?: string): Promise<Locator> {
    const toast = this.page.locator('.p-toast-message');
    await toast.waitFor({ state: 'visible' });

    if (expectedText) {
      await toast
        .filter({ hasText: expectedText })
        .waitFor({ state: 'visible' });
    }

    return toast;
  }

  async closeToasts(): Promise<void> {
    const closeButtons = this.page.locator('.p-toast-icon-close');
    const count = await closeButtons.count();

    for (let i = 0; i < count; i++) {
      await closeButtons.nth(i).click();
    }
  }

  async waitForOverlay(): Promise<void> {
    await this.page.waitForSelector('.p-component-overlay', {
      state: 'visible',
    });
  }

  async waitForSidebar(): Promise<Locator> {
    const sidebar = this.page.locator('.p-sidebar');
    await sidebar.waitFor({ state: 'visible' });
    return sidebar;
  }

  async closeSidebar(): Promise<void> {
    await this.page.locator('.p-sidebar-mask').click();
    await this.page.locator('.p-sidebar').waitFor({ state: 'hidden' });
  }

  async takeScreenshot(name: string): Promise<void> {
    await this.page.screenshot({
      path: `e2e/screenshots/${name}.png`,
      fullPage: true,
    });
  }
}
