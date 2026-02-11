import { defineConfig } from 'vitest/config';
import path from 'path';
import angular from '@analogjs/vite-plugin-angular';

export default defineConfig({
  plugins: [angular()],
  test: {
    // Test environment - jsdom for Angular components
    environment: 'jsdom',
    
    // Environment options for jsdom
    environmentOptions: {
      jsdom: {
        resources: 'usable',
        pretendToBeVisual: true,
      },
    },
    
    // Include patterns - all spec files in src/
    include: ['src/**/*.spec.ts'],
    
    // Exclude E2E tests
    exclude: ['ui-tests/**/*', 'node_modules/**/*'],
    
    // Enable globals (describe, it, expect, etc.)
    globals: true,
    
    // Setup files for Angular Zone.js and global mocks
    setupFiles: ['@analogjs/vitest-angular/setup-zone', './src/test-globals.ts'],
    
    // Coverage configuration
    coverage: {
      provider: 'v8',
      reporter: ['text', 'json', 'html', 'lcov'],
      include: ['src/app/**/*.ts'],
      exclude: [
        'src/**/*.spec.ts',
        'src/**/*.module.ts',
        'src/test-setup.ts',
        'src/main.ts',
        'src/environments/**/*'
      ],
      thresholds: {
        lines: 60,
        functions: 60,
        branches: 60,
        statements: 60
      }
    },
    
    // Server options for Angular testing
    server: {
      deps: {
        inline: ['@angular/**', 'vscroll', 'ngx-ui-scroll', '@microsoft/signalr', 'ngx-simplemde']
      }
    },
  },
  
  // Resolve configuration for Angular
  resolve: {
    alias: {
      '@app': path.resolve(__dirname, './src/app'),
      'src': path.resolve(__dirname, './src'),
    },
  },
});
