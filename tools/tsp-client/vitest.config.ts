import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    testTimeout: 60_000,
    exclude: ["test/examples/**", "dist/**", "**/node_modules/**"],
  },
});
