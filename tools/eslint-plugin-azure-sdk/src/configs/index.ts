/**
 * @fileoverview Definition of configs
 * @author Arpan Laha
 */

export = {
  recommended: {
    plugins: ["@azure/azure-sdk"],
    env: {
      node: true
    },
    parser: "@typescript-eslint/parser",
    rules: {
      "@azure/azure-sdk/ts-config-allowsyntheticdefaultimports": "error",
      "@azure/azure-sdk/ts-config-declaration": "error",
      "@azure/azure-sdk/ts-config-esmoduleinterop": "error",
      "@azure/azure-sdk/ts-config-exclude": "error",
      "@azure/azure-sdk/ts-config-forceconsistentcasinginfilenames": "error",
      "@azure/azure-sdk/ts-config-importhelpers": "error",
      "@azure/azure-sdk/ts-config-isolatedmodules": "warn",
      "@azure/azure-sdk/ts-config-lib": "error",
      "@azure/azure-sdk/ts-config-module": "error",
      "@azure/azure-sdk/ts-config-no-experimentaldecorators": "error",
      "@azure/azure-sdk/ts-config-sourcemap": "error",
      "@azure/azure-sdk/ts-config-strict": "error",
      "@azure/azure-sdk/ts-package-json-author": "error",
      "@azure/azure-sdk/ts-package-json-bugs": "error",
      "@azure/azure-sdk/ts-package-json-homepage": "error",
      "@azure/azure-sdk/ts-package-json-keywords": "error",
      "@azure/azure-sdk/ts-package-json-license": "error",
      "@azure/azure-sdk/ts-package-json-name": "error",
      "@azure/azure-sdk/ts-package-json-repo": "error",
      "@azure/azure-sdk/ts-package-json-required-scripts": "error",
      "@azure/azure-sdk/ts-package-json-sideeffects": "error"
    }
  }
};
