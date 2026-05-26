# skill-eval (CI-only npm project)

This folder exists solely to give the [Skill Evaluations pipeline](../../.azure-pipelines/skill-eval.yml) a tiny `package.json` it can `npm ci` against, so `@microsoft/vally-cli` is restored from the authenticated DevOps npm feed instead of installed globally from public npm.

- Do not add runtime code here.
- The only dependency should be `@microsoft/vally-cli`, pinned to the version CI should validate skills with.
- `package-lock.json` must be committed so `npm ci` is deterministic.

## Updating the Vally CLI version

1. Bump `@microsoft/vally-cli` in `package.json`.
2. Run `npm install` locally to refresh `package-lock.json`.
3. Commit both files in the same PR.

## Local skill linting

Contributors can keep using a global install for local iteration:

```sh
npm install -g @microsoft/vally-cli@<version>
vally lint .
```

Only CI is required to go through the DevOps feed.
