# typespec-bump-deps

Tool to grab the latest `@next` version for all TypeSpec packages.

## Usage

```
npx @azure-tools/typespec-bump-deps <package_json_file> [options]
```

For the positional argument `<package_json_file>`, it can accept multiple "package.json" or "rush.json" files.

## Options

- `add-rush-overrides`: Add an "overrides" block to rush.json of Rush.
- `add-npm-overrides`: Add an "overrides" block to package.json of npm.
- `use-peer-ranges`: Use semver ranges on the "peerDependencies" block.
