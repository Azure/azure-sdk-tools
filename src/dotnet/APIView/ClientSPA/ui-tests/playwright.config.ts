import { defineConfig, devices } from '@playwright/test';

/**
 * Playwright configuration for APIView ClientSPA UI tests.
 */
export default defineConfig({
  // Test directory
  testDir: './tests',

  // Run tests in files in parallel
  fullyParallel: true,

  // Reporter configuration
  reporter: [
    ['html', { open: 'never', outputFolder: 'playwright-report' }],
    ['list'],
    // JUnit reporter for Azure DevOps integration
    ['junit', { outputFile: 'test-results/junit-results.xml' }]
  ],

  // Timeout for each test
  timeout: 30000,

  // Shared settings for all projects
  use: {
    // Base URL for the Angular app
    baseURL: 'https://localhost:4200',

    // Ignore HTTPS errors (Angular dev server uses self-signed cert)
    ignoreHTTPSErrors: true,

    // Collect trace when retrying the failed test
    trace: 'on-first-retry',

    // Screenshot on failure
    screenshot: 'only-on-failure',

    // Video recording (useful for debugging flaky tests)
    video: 'retain-on-failure',
  },

  // Configure projects for different browsers
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],

  // Automatically start the Angular dev server before tests
  // This ensures tests work both locally and in CI pipelines
  webServer: {
    command: 'npm run start -- --ssl',
    url: 'https://localhost:4200',
    reuseExistingServer: !process.env['CI'], // Reuse existing server locally, start fresh in CI
    timeout: 120000, // 2 minutes to start the dev server
    ignoreHTTPSErrors: true,
    cwd: '..', // Run from ClientSPA directory (parent of ui-tests)
  },
});
