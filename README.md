# eslint-plugin-azure

Linting rules for the JavaScript/TypeScript Azure SDK - derived from the [Azure SDK Design Guidelines for TypeScript](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html
).

## Installation

You'll first need to install [ESLint](http://eslint.org):

```
$ npm i eslint --save-dev
```

Next, install `eslint-plugin-azure`:

```
$ npm install eslint-plugin-azure --save-dev
```

**Note:** If you installed ESLint globally (using the `-g` flag) then you must also install `eslint-plugin-azure` globally.

## Usage

Add `azure` to the `plugins` section of your `.eslintrc` configuration file. You can omit the `eslint-plugin-` prefix:

```json
{
  "plugins": ["azure"]
}
```

For all rules to be enforced according to the standards set by the Design Guidelines, add this plugin's `recommended` configuration to the `extends` section of your `eslintrc` configuration file as follows:

```json
{
  "extends": [
    "plugin:azure/recommended"
  ]
}
```

If you need to modify or disable specific rules, you can do so in the `rules` section of your `eslintrc` configuration file:

```json
{
  "rules": {
    "azure/rule-name": "off"
  }
}
```

## Supported Rules

- [ts-config-allowsyntheticdefaultimports](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-config-allowsyntheticdefaultimports)
- [ts-config-declaration](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-config-declaration)
- [ts-config-esmoduleinterop](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-config-esmoduleinterop)
- [ts-config-forceconsistentcasinginfilenames](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-config-forceconsistentcasinginfilenames)
- [ts-config-importhelpers](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-config-importhelpers)
- [ts-config-isolatedmodules](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-config-isolatedmodules)
- [ts-config-module](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-config-module)
- [ts-config-no-experimentaldecorators](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-config-no-experimentaldecorators)
- [ts-config-strict](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-config-strict)
- [ts-package-json-author](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-package-json-author)
- [ts-package-json-bugs](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-package-json-bugs)
- [ts-package-json-license](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-package-json-license)
- [ts-package-json-repo](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-package-json-repo)
- [ts-package-json-sideeffects](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-package-json-sideeffects)