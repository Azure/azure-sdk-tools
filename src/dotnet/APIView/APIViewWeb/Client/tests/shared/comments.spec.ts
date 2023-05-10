import { test, expect } from '@playwright/test';
import * as path from 'path';


test('has title', async ({ page }) => {
  const testHtml = path.resolve(`${__dirname}/comments.spec.html`);
  await page.goto(testHtml);

  // Expect a title "to contain" a substring.
  await expect(page).toHaveTitle(/Comments Spec/);
});