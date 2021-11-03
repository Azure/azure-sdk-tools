# Prerequisites

- [nodejs](https://nodejs.org/en/)
- [npm](https://www.npmjs.com/)

# Build, Test and Release

## Dependency
```
npm install -g "@microsoft/rush"
rush install
```

## Build all packages

```
rush rebuild
```

## Test all packages

```
rush test
```

## Release packages
1. With each change, generate a change file
```
rush change
```
- Judge which package you changed.
- Write changelog for changes.
- Decide change type: major/minor/hotfix.
- You can execute for serveral time to add different change files.
- Commit change within your PR.
2. Before release, gather all changes, pump version and generate changelogs
```
rush publish --apply
```
- Verify auto-generated verson and changelog.
- Commit and create PR and wait for merge.
3. Tag release and execute internal pipeline to release to npm registry.
