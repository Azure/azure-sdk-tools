/**
 * @fileoverview Definition of configs
 * @author Arpan Laha
 */

export = {
  recommended: {
    plugins: ["azure"],
    env: {
      node: true
    },
    parser: "@typescript-eslint/parser",
    rules: {
      "azure/ts-config-allowsyntheticdefaultimports": "error",
      "azure/ts-config-declaration": "error",
      "azure/ts-config-esmoduleinterop": "error",
      "azure/ts-config-exclude": "error",
      "azure/ts-config-forceconsistentcasinginfilenames": "error",
      "azure/ts-config-importhelpers": "error",
      "azure/ts-config-isolatedmodules": "warn",
      "azure/ts-config-lib": "error",
      "azure/ts-config-module": "error",
      "azure/ts-config-no-experimentaldecorators": "error",
      "azure/ts-config-sourcemap": "error",
      "azure/ts-config-strict": "error",
      "azure/ts-package-json-author": "error",
      "azure/ts-package-json-bugs": "error",
      "azure/ts-package-json-keywords": "error",
      "azure/ts-package-json-license": "error",
      "azure/ts-package-json-repo": "error",
      "azure/ts-package-json-sideeffects": "error"
    }
  }
};
