import js from "@eslint/js";
import tseslint from "typescript-eslint";
import prettierRecommended from "eslint-plugin-prettier/recommended";
import simpleImportSort from "eslint-plugin-simple-import-sort";
import globals from "globals";

export default tseslint.config(
    // Global ignores
    {
        ignores: ["dist/**", "node_modules/**", "**/*.d.ts", "coverage/**", "swagger/**", "**/*.js", "**/*.mjs", "**/*.cjs"],
    },
    // ESLint recommended
    js.configs.recommended,
    // TypeScript ESLint recommended
    ...tseslint.configs.recommended,
    // Prettier (must be last of the shared configs)
    prettierRecommended,
    // Custom TypeScript config
    {
        files: ["**/*.ts"],
        languageOptions: {
            globals: {
                ...globals.browser,
                ...globals.es2021,
            },
            parserOptions: {
                project: ["./tsconfig.json"],
            },
        },
        plugins: {
            "simple-import-sort": simpleImportSort,
        },
        rules: {
            "prefer-template": "off",
            "linebreak-style": "off",
            "indent": "off",
            "no-console": "off",
            "simple-import-sort/imports": "error",
            "simple-import-sort/exports": "error",
            "@typescript-eslint/no-explicit-any": "off",
            "@typescript-eslint/explicit-module-boundary-types": "off",
            "eqeqeq": "error",
            "@typescript-eslint/naming-convention": [
                "error",
                {
                    "selector": ["class", "interface", "enum", "typeParameter", "typeLike", "default"],
                    "format": ["PascalCase"],
                    "filter": {
                        "regex": "^_$",
                        "match": false,
                    },
                },
                {
                    "selector": ["import"],
                    "format": null,
                    "filter": {
                        "regex": "^(_|child_process)$",
                        "match": true,
                    },
                },
                {
                    "selector": ["import"],
                    "format": ["camelCase", "PascalCase"],
                },
                {
                    "selector": ["enumMember", "variable", "parameter", "function", "method", "property", "memberLike"],
                    "format": ["camelCase"],
                    "filter": {
                        "regex": "^_$",
                        "match": false,
                    },
                },
                {
                    "selector": ["default"],
                    "modifiers": ["global", "const"],
                    "format": ["UPPER_CASE"],
                    "filter": {
                        "regex": "^_$",
                        "match": false,
                    },
                },
                {
                    "selector": ["objectLiteralProperty"],
                    "format": null,
                },
            ],
            "new-parens": "error",
            "no-new-wrappers": "error",
            "no-array-constructor": "off",
            "no-throw-literal": "error",
            "guard-for-in": "error",
            "curly": ["error", "all"],
            "default-case": "error",
            "prefer-arrow-callback": "error",
            "func-style": ["error", "declaration"],
            "@typescript-eslint/consistent-type-assertions": [
                "warn",
                {
                    "assertionStyle": "as",
                    "objectLiteralTypeAssertions": "allow-as-parameter",
                },
            ],
            "semi": ["error", "always"],
            // Replaces import/no-default-export
            "no-restricted-syntax": [
                "error",
                {
                    "selector": "ExportDefaultDeclaration",
                    "message": "Prefer named exports over default exports.",
                },
            ],
            "no-restricted-imports": [
                "error",
                {
                    "patterns": [
                        {
                            "regex": "^/",
                            "message": "Do not use absolute path imports.",
                        },
                    ],
                },
            ],
            "@typescript-eslint/no-unnecessary-type-assertion": "error",
            "@typescript-eslint/array-type": ["error", { "default": "array-simple" }],
            "arrow-parens": ["error", "always"],
            "@typescript-eslint/no-unused-vars": ["error", { "varsIgnorePattern": "^_", "argsIgnorePattern": "^_" }],
        },
    },
);
