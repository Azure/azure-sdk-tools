import { defineConfig } from 'vitest/config';

export default defineConfig({
  test: {
    environment: 'node',
    coverage: {
      provider: 'v8',
      reporter: ['text', 'json', 'html'],
      include: ['src/**/*.ts'],
    },
    outputFile: {
      junit: './test-results.xml',
    },
    reporters: 'default',
    exclude: ['node_modules', 'dist/test', 'dist'],
    silent: false,
  },
  esbuild: {
    sourcemap: true,
  },
  server: {
    watch: {
      ignored: [],
    },
  }
});
