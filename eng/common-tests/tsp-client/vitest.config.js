import { cpus } from "os";
import { defineConfig } from "vitest/config";

export default defineConfig({
  esbuild: {
    // Ignore tsconfig.json, since it's only used for type checking, and causes
    // a warning if vitest tries to load it
    // @ts-expect-error: 'tsConfig' does not exist in type 'ESBuildOptions'
    tsConfig: false,
  },
  test: {
    // Default is 5, but increasing to num cpus can improve perf on many-core machines
    maxConcurrency: cpus().length,
    testTimeout: 240_000,
  },
});
