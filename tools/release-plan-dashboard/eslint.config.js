import eslint from "@eslint/js";
import { defineConfig } from "eslint/config";
import globals from "globals";

export default defineConfig(
  eslint.configs.recommended,
  {
    languageOptions: {
      globals: {
        ...globals.node,
      },
      sourceType: "module",
    },
  },
  {
    files: ["public/**/*.js"],
    languageOptions: {
      globals: {
        ...globals.browser,
        Alpine: "readonly",
      },
      sourceType: "script",
    },
    rules: {
      // Many functions/vars are called from HTML event handlers or Alpine bindings
      "no-unused-vars": "warn",
      "no-useless-assignment": "warn",
    },
  },
  {
    files: ["tests/**/*.js"],
    languageOptions: {
      globals: {
        ...globals.node,
      },
    },
  },
  {
    ignores: ["coverage/**", "node_modules/**"],
  },
);
