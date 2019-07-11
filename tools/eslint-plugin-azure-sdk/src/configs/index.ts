/**
 * @fileoverview Definition of configs
 * @author Arpan Laha
 */

export = {
  recommended: {
    plugins: ["@azuresdktools/azure-sdk"],
    env: {
      node: true
    },
    parser: "@typescript-eslint/parser",
    rules: {
      "@azuresdktools/azure-sdk/github-source-headers": "error",
      "@azuresdktools/azure-sdk/ts-config-allowsyntheticdefaultimports":
        "error",
      "@azuresdktools/azure-sdk/ts-config-declaration": "error",
      "@azuresdktools/azure-sdk/ts-config-esmoduleinterop": "error",
      "@azuresdktools/azure-sdk/ts-config-exclude": "error",
      "@azuresdktools/azure-sdk/ts-config-forceconsistentcasinginfilenames":
        "error",
      "@azuresdktools/azure-sdk/ts-config-importhelpers": "error",
      "@azuresdktools/azure-sdk/ts-config-isolatedmodules": "warn",
      "@azuresdktools/azure-sdk/ts-config-lib": "error",
      "@azuresdktools/azure-sdk/ts-config-module": "error",
      "@azuresdktools/azure-sdk/ts-config-moduleresolution": "error",
      "@azuresdktools/azure-sdk/ts-config-no-experimentaldecorators": "error",
      "@azuresdktools/azure-sdk/ts-config-sourcemap": "error",
      "@azuresdktools/azure-sdk/ts-config-strict": "error",
      "@azuresdktools/azure-sdk/ts-config-target": "error",
      "@azuresdktools/azure-sdk/ts-error-handling": "off",
      "@azuresdktools/azure-sdk/ts-modules-only-named": "error",
      "@azuresdktools/azure-sdk/ts-no-const-enums": "warn",
      "@azuresdktools/azure-sdk/ts-package-json-author": "error",
      "@azuresdktools/azure-sdk/ts-package-json-bugs": "error",
      "@azuresdktools/azure-sdk/ts-package-json-engine-is-present": "error",
      "@azuresdktools/azure-sdk/ts-package-json-files-required": "error",
      "@azuresdktools/azure-sdk/ts-package-json-homepage": "error",
      "@azuresdktools/azure-sdk/ts-package-json-keywords": "error",
      "@azuresdktools/azure-sdk/ts-package-json-license": "error",
      "@azuresdktools/azure-sdk/ts-package-json-main-is-cjs": "error",
      "@azuresdktools/azure-sdk/ts-package-json-module": "error",
      "@azuresdktools/azure-sdk/ts-package-json-name": "error",
      "@azuresdktools/azure-sdk/ts-package-json-repo": "error",
      "@azuresdktools/azure-sdk/ts-package-json-required-scripts": "error",
      "@azuresdktools/azure-sdk/ts-package-json-sideeffects": "error",
      "@azuresdktools/azure-sdk/ts-package-json-types": "error",
      "@azuresdktools/azure-sdk/ts-use-interface-parameters": "warn",
      "@azuresdktools/azure-sdk/ts-versioning-semver": "error",
      "@azuresdktools/azure-sdk/tsdoc-ignore-helpers": "off"
    },
    settings: {
      main: "src/index.ts"
    }
  }
};
