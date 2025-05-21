// vitest.config.ts
// Compatible with Vitest v3.1.2 (latest as of April 21, 2025)
import { defineConfig } from 'vitest/config';
import tsconfigPaths from 'vite-tsconfig-paths';
import path from 'node:path';

export default defineConfig({
  plugins: [
    // Auto-resolve paths from tsconfig.json
    tsconfigPaths(),
  ],
  test: {
    // Enable globals like describe, it, expect
    globals: true,
    environment: 'node',
    // Specify test file patterns
    include: ['test/**/*.{test,spec}.{js,ts,jsx,tsx}'],
    exclude: ['node_modules/', 'dist/'],
    // Coverage configuration
    coverage: {
      provider: 'v8', // Use V8 for faster coverage
      reporter: ['text', 'lcov', 'html'],
      exclude: ['node_modules/', 'test/', 'src/setupTests.ts'],
    },
  },
  resolve: {
    alias: {
      // Map '@' to './src'
      '@': path.resolve(__dirname, 'src'),
    },
  },
});
