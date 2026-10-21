# skill-eval (CI-only npm project)

This folder exists solely to give the [Skill Evaluations GitHub Actions workflow](../../.github/workflows/skill-eval.yml) a tiny `package.json` it can `npm ci` against, so `@microsoft/vally-cli` and its full transitive dependency tree are pinned by the committed `package-lock.json` instead of resolved fresh from semver ranges on every CI run.

- Do not add runtime code here.
- The only dependency should be `@microsoft/vally-cli`, pinned to the version CI should validate skills with.
- `package-lock.json` must be committed so `npm ci` is deterministic.

## Why Vally 0.19.0

- Restores MCP tool calls with the existing repository-scoped GitHub App token. No
	broader token permissions, new credentials or authentication fallback are added.
- Uses the supported Vally/Copilot SDK stack in both evaluator packages instead of
	mixing runtime versions or overriding individual transitive dependencies. Both
	lockfiles retain the SDK's eight platform packages and require Node 22.14+.
- Rejects unsupported eval fields before model execution. The existing capability
	definitions now use `defaults`, and the nightly release-planner definition no
	longer contains unsupported top-level `metadata`.

The controlled normal-runner validation in
[build 6981864](https://dev.azure.com/azure-sdk/internal/_build/results?buildId=6981864)
passed 57/57 GitHub/APIView trials with 83 tool calls using the existing App-token
scope and mock binary. That is evidence for that supported stack, not a claim that
every suite passes.

### Compatibility boundary

The offline schema tests load the existing projects declared in the workflow/tool
and skill configuration files. They fail if discovery reports a parse error or
omits a discovered eval; they do not run models, MCP servers, graders or fixtures.
The current skill pipeline's narrower execution globs are unchanged.

The separately owned TypeSpec author benchmark definitions, runner and
pipelines are not changed by this upgrade. Its 52 definitions still use top-level
`area`, which Vally 0.19 rejects, and its runner consumes the shared evaluator pin.
That remains an explicit merge blocker for the benchmark owner to resolve. The
offline checks do not add that benchmark to the existing project discovery paths,
and do not rewrite or bypass its inputs.

## Updating the Vally CLI version

1. Bump `@microsoft/vally-cli` in `package.json`.
2. Run `npm install --package-lock-only --registry https://registry.npmjs.org/` to refresh `package-lock.json`.
3. Run `npm test` to check configured eval discovery and loading, then run skill lint.
4. Commit both files in the same PR. The workflow's path triggers include `eng/skill-eval/**`, so CI will re-run both checks against the new version automatically.

## Local skill linting

Reproduce exactly what CI does by installing from the same lockfile and invoking the local binary:

```sh
cd eng/skill-eval
npm ci
npm test
cd ../..
./eng/skill-eval/node_modules/.bin/vally lint .
```

These are the same schema-loading and skill-lint checks used by CI. A passing local
run does not validate native runtime availability, App-token authentication, live
services, model-backed evaluations or the separately owned TypeSpec benchmark.

A global install (`npm install -g @microsoft/vally-cli@<version>`) still works for ad-hoc iteration, but it won't match the transitive dependency tree CI uses and isn't a substitute for the steps above when validating a version bump.
