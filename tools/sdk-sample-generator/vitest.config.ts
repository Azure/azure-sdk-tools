// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    testTimeout: 99000,
    reporters: ["default"],
    watch: false,
    include: ["test/**/*.spec.ts"],
    typecheck: {
      enabled: true,
      include: ["./test/**/*.spec.ts"],
      tsconfig: "./tsconfig.test.json"
    },
  },
});
