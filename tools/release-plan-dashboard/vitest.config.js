import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    coverage: {
      provider: "v8",
      include: ["lib/**/*.js", "routes/**/*.js", "server.js"],
      thresholds: {
        branches: 70,
        functions: 90,
        lines: 90,
        statements: 90,
      },
    },
  },
});
