# eslint-plugin-azure-sdk

An ESLint plugin enforcing [design guidelines for the JavaScript/TypeScript Azure SDK](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html).

## Installation

You'll first need to install [ESLint](http://eslint.org):

```shell
npm i eslint --save-dev
```

Next, install `@azure/eslint-plugin-azure-sdk`:

```shell
npm install @azure/eslint-plugin-azure-sdk --save-dev
```

**Note:** If you installed ESLint globally (using the `-g` flag) then you must also install `@azure/eslint-plugin-azure-sdk` globally.

## Usage

Add `@azure/azure-sdk` to the `plugins` section of your `.eslintrc` configuration file. You can omit the `eslint-plugin-` prefix:

```json
{
  "plugins": ["@azure/azure-sdk"]
}
```

Make sure to set your `.eslintrc` configuration file's `parserOptions.project` field to point to the tsconfig file at the root of your project as follows:

```json
{
  "parserOptions": {
    "project": "./tsconfig.json"
  }
}
```

For all rules to be enforced according to the standards set by the Design Guidelines, add this plugin's `recommended` configuration to the `extends` section of your `.eslintrc` configuration file as follows:

```json
{
  "extends": ["plugin:@azure/azure-sdk/recommended"]
}
```

If the main TypeScript entrypoint to your package is not in `src/index.ts`, set `settings.main` in your `.eslintrc` configuration file to the entrypoint as follows (for example, if the entrypoint is `index.ts`):

```json
{
  "settings": {
    "main": "index.ts"
  }
}
```

If you need to modify or disable specific rules, you can do so in the `rules` section of your `.eslintrc` configuration file:

```json
{
  "rules": {
    "@azure/azure-sdk/rule-name": "off"
  }
}
```

For example, if you are not targeting Node, disable `ts-config-moduleresolution` as follows:

```json
{
  "rules": {
    "@azure/azure-sdk/ts-config-moduleresolution": "off"
  }
}
```

Some rules (see table below) are fixable using the `--fix` ESLint option.

## Supported Rules

### Key

| Symbol             | Meaning                                         |
| ------------------ | ----------------------------------------------- |
| :heavy_check_mark: | Fixable and enabled                             |
| :soon:             | Fixable but disabled (pending ESLint 6 support) |
| :x:                | Not fixable                                     |

| Rule                                                                                                                                  | Fixable            |
| ------------------------------------------------------------------------------------------------------------------------------------- | ------------------ |
| [github-source-headers](/tools/eslint-plugin-azure-sdk/docs/rules/github-source-headers.md)                                           | :heavy_check_mark: |
| [ts-apisurface-standardized-verbs](/tools/eslint-plugin-azure-sdk/docs/rules/ts-apisurface-standardized-verbs.md)                     | :x:                |
| [ts-apisurface-supportcancellation](/tools/eslint-plugin-azure-sdk/docs/rules/ts-apisurface-supportcancellation.md)                   | :x:                |
| [ts-config-allowsyntheticdefaultimports](/tools/eslint-plugin-azure-sdk/docs/rules/ts-config-allowsyntheticdefaultimports.md)         | :soon:             |
| [ts-config-declaration](/tools/eslint-plugin-azure-sdk/docs/rules/ts-config-declaration.md)                                           | :soon:             |
| [ts-config-esmoduleinterop](/tools/eslint-plugin-azure-sdk/docs/rules/ts-config-esmoduleinterop.md)                                   | :soon:             |
| [ts-config-exclude](/tools/eslint-plugin-azure-sdk/docs/rules/ts-config-exclude.md)                                                   | :soon:             |
| [ts-config-forceconsistentcasinginfilenames](/tools/eslint-plugin-azure-sdk/docs/rules/ts-config-forceconsistentcasinginfilenames.md) | :soon:             |
| [ts-config-importhelpers](/tools/eslint-plugin-azure-sdk/docs/rules/ts-config-importhelpers.md)                                       | :soon:             |
| [ts-config-lib](/tools/eslint-plugin-azure-sdk/docs/rules/ts-config-lib.md)                                                           | :soon:             |
| [ts-config-module](/tools/eslint-plugin-azure-sdk/docs/rules/ts-config-module.md)                                                     | :soon:             |
| [ts-config-moduleresolution](/tools/eslint-plugin-azure-sdk/docs/rules/ts-config-moduleresolution.md)                                 | :soon:             |
| [ts-config-no-experimentaldecorators](/tools/eslint-plugin-azure-sdk/docs/rules/ts-config-no-experimentaldecorators.md)               | :soon:             |
| [ts-config-sourcemap](/tools/eslint-plugin-azure-sdk/docs/rules/ts-config-sourcemap.md)                                               | :soon:             |
| [ts-config-strict](/tools/eslint-plugin-azure-sdk/docs/rules/ts-config-strict.md)                                                     | :soon:             |
| [ts-config-target](/tools/eslint-plugin-azure-sdk/docs/rules/ts-config-target.md)                                                     | :x:                |
| [ts-doc-internal](/tools/eslint-plugin-azure-sdk/docs/rules/ts-doc-internal.md)                                                       | :x:                |
| [ts-error-handling](/tools/eslint-plugin-azure-sdk/docs/rules/ts-error-handling.md)                                                   | :x:                |
| [ts-modules-only-named](/tools/eslint-plugin-azure-sdk/docs/rules/ts-modules-only-named.md)                                           | :x:                |
| [ts-naming-drop-noun](/tools/eslint-plugin-azure-sdk/docs/rules/ts-naming-drop-noun.md)                                               | :x:                |
| [ts-naming-options](/tools/eslint-plugin-azure-sdk/docs/rules/ts-naming-options.md)                                                   | :x:                |
| [ts-naming-subclients](/tools/eslint-plugin-azure-sdk/docs/rules/ts-naming-subclients.md)                                             | :x:                |
| [ts-no-const-enums](/tools/eslint-plugin-azure-sdk/docs/rules/ts-no-const-enums.md)                                                   | :heavy_check_mark: |
| [ts-no-namespaces](/tools/eslint-plugin-azure-sdk/docs/rules/ts-no-namespaces.md)                                                     | :x:                |
| [ts-package-json-author](/tools/eslint-plugin-azure-sdk/docs/rules/ts-package-json-author.md)                                         | :soon:             |
| [ts-package-json-bugs](/tools/eslint-plugin-azure-sdk/docs/rules/ts-package-json-bugs.md)                                             | :soon:             |
| [ts-package-json-engine-is-present](/tools/eslint-plugin-azure-sdk/docs/rules/ts-package-json-engine-is-present.md)                   | :soon:             |
| [ts-package-json-files-required](/tools/eslint-plugin-azure-sdk/docs/rules/ts-package-json-files-required.md)                         | :soon:             |
| [ts-package-json-homepage](/tools/eslint-plugin-azure-sdk/docs/rules/ts-package-json-homepage.md)                                     | :x:                |
| [ts-package-json-keywords](/tools/eslint-plugin-azure-sdk/docs/rules/ts-package-json-keywords.md)                                     | :soon:             |
| [ts-package-json-license](/tools/eslint-plugin-azure-sdk/docs/rules/ts-package-json-license.md)                                       | :soon:             |
| [ts-package-json-main-is-cjs](/tools/eslint-plugin-azure-sdk/docs/rules/ts-package-json-main-is-cjs.md)                               | :soon:             |
| [ts-package-json-module](/tools/eslint-plugin-azure-sdk/docs/rules/ts-package-json-module.md)                                         | :soon:             |
| [ts-package-json-name](/tools/eslint-plugin-azure-sdk/docs/rules/ts-package-json-name.md)                                             | :x:                |
| [ts-package-json-repo](/tools/eslint-plugin-azure-sdk/docs/rules/ts-package-json-repo.md)                                             | :soon:             |
| [ts-package-json-required-scripts](/tools/eslint-plugin-azure-sdk/docs/rules/ts-package-json-required-scripts.md)                     | :x:                |
| [ts-package-json-sideeffects](/tools/eslint-plugin-azure-sdk/docs/rules/ts-package-json-sideeffects.md)                               | :soon:             |
| [ts-package-json-types](/tools/eslint-plugin-azure-sdk/docs/rules/ts-package-json-types.md)                                           | :x:                |
| [ts-pagination-list](/tools/eslint-plugin-azure-sdk/docs/rules/ts-pagination-list.md)                                                 | :x:                |
| [ts-use-interface-parameters](/tools/eslint-plugin-azure-sdk/docs/rules/ts-use-interface-parameters.md)                               | :x:                |
| [ts-use-promises](/tools/eslint-plugin-azure-sdk/docs/rules/ts-use-promises.md)                                                       | :x:                |
| [ts-versioning-semver](/tools/eslint-plugin-azure-sdk/docs/rules/ts-versioning-semver.md)                                             | :x:                |
