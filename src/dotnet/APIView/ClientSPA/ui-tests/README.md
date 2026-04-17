# APIView ClientSPA E2E Tests

This folder contains Playwright end-to-end tests for the Angular ClientSPA.

## Prerequisites

1. **Node.js 18+** installed
2. **Playwright browsers** installed (run once):
   ```bash
   npx playwright install chromium
   ```

## Running Tests

### Step 1: Start the Angular Dev Server

In one terminal, start the Angular application:

```bash
cd src/dotnet/APIView/ClientSPA
npm run start -- --ssl
```

Wait for the server to be ready at `https://localhost:4200`.

### Step 2: Run the Tests

In another terminal, run the tests:

```bash
# Run all tests (headless)
npm run e2e

# Run tests with browser visible
npm run e2e:headed

# Run tests with interactive UI (great for debugging)
npm run e2e:debug

# Run a specific test file
npx playwright test --config=ui-tests/playwright.config.ts ui-tests/tests/review-page/basic.spec.ts

# Run tests matching a pattern
npx playwright test --config=ui-tests/playwright.config.ts -g "should load"
```

### Step 3: View Test Report

After running tests, view the HTML report:

```bash
npm run e2e:report
```

## VS Code Integration

Install the [Playwright Test for VS Code](https://marketplace.visualstudio.com/items?itemName=ms-playwright.playwright) extension for:

- Run/debug individual tests from the editor
- Set breakpoints in test code
- View test results inline
- Record new tests

## Debugging Tips

1. **Use headed mode** to see what's happening:
   ```bash
   npm run e2e:headed
   ```

2. **Use UI mode** for interactive debugging:
   ```bash
   npm run e2e:debug
   ```

3. **Add `await page.pause()`** in your test to pause execution and inspect the page.

4. **Check screenshots** in `test-results/` folder when tests fail.

5. **Enable trace** to get detailed step-by-step recording:
   ```bash
   npx playwright test --config=ui-tests/playwright.config.ts --trace on
   ```
