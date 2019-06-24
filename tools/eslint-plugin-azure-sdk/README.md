# eslint-plugin-azure-sdk

An ESLint plugin enforcing [design guidelines for the JavaScript/TypeScript Azure SDK](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html).

## Installation

You'll first need to install [ESLint](http://eslint.org):

```shell
npm i eslint --save-dev
```

Next, install `@ts-common/eslint-plugin-azure-sdk`:

```shell
npm install @ts-common/eslint-plugin-azure-sdk --save-dev
```

**Note:** If you installed ESLint globally (using the `-g` flag) then you must also install `@ts-common/eslint-plugin-azure-sdk` globally.

## Usage

Add `@ts-common/azure-sdk` to the `plugins` section of your `.eslintrc` configuration file. You can omit the `eslint-plugin-` prefix:

```json
{
  "plugins": ["@ts-common/azure-sdk"]
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

For all rules to be enforced according to the standards set by the Design Guidelines, add this plugin's `recommended` configuration to the `extends` section of your `eslintrc` configuration file as follows:

```json
{
  "extends": ["plugin:@ts-common/azure-sdk/recommended"]
}
```

If you need to modify or disable specific rules, you can do so in the `rules` section of your `eslintrc` configuration file:

```json
{
  "rules": {
    "@ts-common/azure-sdk/rule-name": "off"
  }
}
```

## Supported Rules

- [ts-config-allowsyntheticdefaultimports](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-config-allowsyntheticdefaultimports)
- [ts-config-declaration](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-config-declaration)
- [ts-config-esmoduleinterop](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-config-esmoduleinterop)
- [ts-config-exclude](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-config-exclude)
- [ts-config-forceconsistentcasinginfilenames](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-config-forceconsistentcasinginfilenames)
- [ts-config-importhelpers](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-config-importhelpers)
- [ts-config-isolatedmodules](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-config-isolatedmodules)
- [ts-config-lib](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-config-lib)
- [ts-config-module](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-config-module)
- [ts-config-no-experimentaldecorators](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-config-no-experimentaldecorators)
- [ts-config-sourcemap](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-config-sourcemap)
- [ts-config-strict](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-config-strict)
- [ts-package-json-author](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-package-json-author)
- [ts-package-json-bugs](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-package-json-bugs)
- [ts-package-json-homepage](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-package-json-homepage)
- [ts-package-json-keywords](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-package-json-keywords)
- [ts-package-json-license](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-package-json-license)
- [ts-package-json-name](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-package-json-name)
- [ts-package-json-repo](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-package-json-repo)
- [ts-package-json-required-scripts](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-package-json-required-scripts)
- [ts-package-json-sideeffects](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-package-json-sideeffects)
