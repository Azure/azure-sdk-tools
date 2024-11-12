import { defineConfig } from 'vitest/config';

export default defineConfig({
  test: {
    environment: 'node',
    isolate: false,
    coverage: {
      reporter: ['cobertura', 'json', 'text'],
      include: ['src/**/*.ts'],
    },
    outputFile: {
      junit: './test-results.xml',
    },
    exclude: ['node_modules', 'dist/test', 'dist'],
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
