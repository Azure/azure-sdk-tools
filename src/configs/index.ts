/**
 * @fileoverview Definition of configs
 * @author Arpan Laha
 */

export = {
  recommended: {
    plugins: ["@ts-common/azure-sdk"],
    env: {
      node: true
    },
    parser: "@typescript-eslint/parser",
    rules: {
      "@ts-common/azure-sdk/ts-config-allowsyntheticdefaultimports": "error",
      "@ts-common/azure-sdk/ts-config-declaration": "error",
      "@ts-common/azure-sdk/ts-config-esmoduleinterop": "error",
      "@ts-common/azure-sdk/ts-config-exclude": "error",
      "@ts-common/azure-sdk/ts-config-forceconsistentcasinginfilenames":
        "error",
      "@ts-common/azure-sdk/ts-config-importhelpers": "error",
      "@ts-common/azure-sdk/ts-config-isolatedmodules": "warn",
      "@ts-common/azure-sdk/ts-config-lib": "error",
      "@ts-common/azure-sdk/ts-config-module": "error",
      "@ts-common/azure-sdk/ts-config-no-experimentaldecorators": "error",
      "@ts-common/azure-sdk/ts-config-sourcemap": "error",
      "@ts-common/azure-sdk/ts-config-strict": "error",
      "@ts-common/azure-sdk/ts-package-json-author": "error",
      "@ts-common/azure-sdk/ts-package-json-bugs": "error",
      "@ts-common/azure-sdk/ts-package-json-homepage": "error",
      "@ts-common/azure-sdk/ts-package-json-keywords": "error",
      "@ts-common/azure-sdk/ts-package-json-license": "error",
      "@ts-common/azure-sdk/ts-package-json-name": "error",
      "@ts-common/azure-sdk/ts-package-json-repo": "error",
      "@ts-common/azure-sdk/ts-package-json-required-scripts": "error",
      "@ts-common/azure-sdk/ts-package-json-sideeffects": "error"
    }
  }
};
