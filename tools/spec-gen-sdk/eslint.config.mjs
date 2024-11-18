import globals from "globals";
import pluginJs from "@eslint/js";
import tsPlugin from "@typescript-eslint/eslint-plugin";
import tsParser from "@typescript-eslint/parser";

/** @type {import('eslint').Linter.Config[]} */
export default [
  {
    files: ["**/*.{js,mjs,cjs,ts}"],
    languageOptions: {
      globals: {
        ...globals.node
      },      
      parser: tsParser,
      parserOptions: {
        ecmaVersion: "latest", // Use the latest ECMAScript features
        sourceType: "module",
        project: "./tsconfig.json", // Ensure this points to your tsconfig file
      },
    },
    ignores: ["test/**/*", "jest.config.js", "eslint.config.mjs"],
    plugins: {
      "@typescript-eslint": tsPlugin,
    },
    rules: {
      ...pluginJs.configs.recommended.rules,
      ...tsPlugin.configs.recommended.rules,
      "@typescript-eslint/ban-ts-comment": "error",
      "@typescript-eslint/no-unused-expressions": ["error", { "allowShortCircuit": true, "allowTernary": true }]
    },
  },
];