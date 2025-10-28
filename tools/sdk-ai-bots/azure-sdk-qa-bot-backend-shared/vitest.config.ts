import { defineConfig } from 'vitest/config';

export default defineConfig({
  test: {
    globals: true, // Use Vitest's global APIs (describe, it, expect, etc.)
    environment: 'node', // Specify the test environment (e.g., node, jsdom)
    // reporters: ['default', 'html'], // Optional: configure test reporters
    // coverage: { // Optional: configure code coverage
    //   provider: 'v8', // or 'istanbul'
    //   reporter: ['text', 'json', 'html'],
    //   reportsDirectory: './coverage',
    //   include: ['src/**/*.ts'], // Adjust to match your source file structure
    //   exclude: [ // Optional: exclude files from coverage
    //     'src/test/**',
    //     'src/**/*.d.ts',
    //     'src/**/index.ts', // Example: exclude barrel files if not testable
    //   ],
    // },
    // setupFiles: ['./src/test/setup.ts'], // Optional: path to a setup file for tests
    // testTimeout: 5000, // Optional: default timeout for tests
    // hookTimeout: 10000, // Optional: default timeout for hooks
    // You can add more Vitest specific configurations here
    // For example, if you need to transform specific files or mock modules globally
  },
  // If you are using TypeScript and need path aliases from tsconfig.json
  // resolve: {
  //   alias: {
  //     // Example: '@/*': path.resolve(__dirname, './src/*')
  //   },
  // },
});
