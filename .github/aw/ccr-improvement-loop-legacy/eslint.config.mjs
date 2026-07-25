import js from "@eslint/js";
import tseslint from "typescript-eslint";

export default tseslint.config(
  {
    ignores: [
      "node_modules",
      "coverage",
      "pr-cache",
      "run-cache",
      "dashboard/vendor",
    ],
  },
  js.configs.recommended,
  ...tseslint.configs.strictTypeChecked,
  ...tseslint.configs.stylisticTypeChecked,
  {
    languageOptions: {
      parserOptions: {
        projectService: {
          allowDefaultProject: ["*.mjs", "*.config.ts"],
        },
        tsconfigRootDir: import.meta.dirname,
      },
    },
    rules: {
      "no-useless-rename": "error",
      eqeqeq: ["error", "always", { null: "ignore" }],
      "@typescript-eslint/consistent-type-imports": [
        "error",
        { disallowTypeAnnotations: false },
      ],
      "@typescript-eslint/restrict-template-expressions": [
        "error",
        { allowNumber: true },
      ],
      "@typescript-eslint/no-unused-vars": [
        "error",
        { argsIgnorePattern: "^_", varsIgnorePattern: "^_" },
      ],
    },
  },
  {
    files: ["tests/**/*.ts"],
    rules: {
      "@typescript-eslint/no-non-null-assertion": "off",
      "@typescript-eslint/no-unnecessary-condition": "off",
    },
  },
  {
    files: ["*.mjs", "*.config.ts"],
    extends: [tseslint.configs.disableTypeChecked],
  },
  {
    // Browser dashboard: plain ESM, kept out of the Node tsconfig project.
    // disableTypeChecked drops type-aware rules + the project requirement so
    // these files don't destabilize the existing typecheck/lint pipeline.
    files: ["dashboard/**/*.mjs"],
    extends: [tseslint.configs.disableTypeChecked],
    languageOptions: {
      globals: {
        // browser
        document: "readonly",
        window: "readonly",
        fetch: "readonly",
        HTMLElement: "readonly",
        HTMLCanvasElement: "readonly",
        HTMLInputElement: "readonly",
        // vendored Chart.js UMD global
        Chart: "readonly",
        // shared
        console: "readonly",
      },
    },
  },
);
