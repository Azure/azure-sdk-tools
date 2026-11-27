import { configDefaults, defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    name: "azure-typespec-assessment",
    fileParallelism: false,
    include: ["tests/**/*.test.ts"],
    coverage: {
      provider: "v8",
      reporter: ["text", "json", "html"],
      exclude: [...(configDefaults.coverage.exclude ?? []), "tests/**"],
    },
    restoreMocks: true,
    testTimeout: 60_000,
  },
});
