# eslint-plugin-azure

Linting rules for the JavaScript/TypeScript Azure SDK

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

Add `azure` to the plugins section of your `.eslintrc` configuration file. You can omit the `eslint-plugin-` prefix:

```json
{
    "plugins": [
        "azure"
    ]
}
```


Then configure the rules you want to use under the rules section.

```json
{
    "rules": {
        "azure/rule-name": 2
    }
}
```

## Supported Rules

* Fill in provided rules here





