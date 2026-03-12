# tsp-client Tests

Integration tests for [`eng/common/tsp-client`](../../common/tsp-client/), verifying `tsp-client init` and `tsp-client update` across multiple SDK languages (Go, Java, JS, .NET, Python).

## Prerequisites

- [Node.js](https://nodejs.org/) (version compatible with the repo)
- [Git](https://git-scm.com/)

## Local Setup

The tests expect all repos to be cloned as **siblings** of the `azure-sdk-tools` root directory. For example:

```
repos/
├── azure-sdk-tools/   (or "tools")
├── azure-rest-api-specs/   (or "specs")
├── azure-sdk-for-js/   (or "js")
├── azure-sdk-for-java/   (or "java")
├── azure-sdk-for-go/   (or "go")
├── azure-sdk-for-net/   (or "net")
└── azure-sdk-for-python/   (or "python")
```

Both long names (e.g. `azure-sdk-for-js`) and short names (e.g. `js`) are supported. Tests for repos that are not cloned locally will be **skipped** automatically.

### 1. Clone repos with sparse checkout

You only need the spec repo and at least one SDK language repo. Sparse checkout keeps the clones small.

**azure-sdk-tools** (if not already cloned):

```sh
git clone --sparse https://github.com/Azure/azure-sdk-tools.git
cd azure-sdk-tools
git sparse-checkout set .github eng/common/tsp-client eng/common-tests/tsp-client
cd ..
```

**azure-rest-api-specs** (required for "inits from local" tests):

```sh
git clone --sparse https://github.com/Azure/azure-rest-api-specs.git
cd azure-rest-api-specs
git sparse-checkout set specification/widget
cd ..
```

**azure-sdk-for-js** (example SDK repo — repeat for other languages as needed):

```sh
git clone --sparse https://github.com/Azure/azure-sdk-for-js.git
cd azure-sdk-for-js
git sparse-checkout set eng sdk/template
cd ..
```

> **Tip:** For Java, Go, .NET, or Python, use the same pattern — replace `azure-sdk-for-js` with the target repo and adjust sparse paths to include `eng` and `sdk/template`.

### 2. Install dependencies

From the `azure-sdk-tools` root, install dependencies for **both** `eng/common/tsp-client` and `eng/common-tests/tsp-client`:

```sh
cd eng/common/tsp-client
npm ci

cd ../../common-tests/tsp-client
npm ci
```

## Running Tests

From `eng/common-tests/tsp-client`:

```sh
# Run all tests
npm run test:ci

# Run tests interactively (watch mode)
npm run test
```

### Filtering by language

Use `--testNamePattern` to run tests for a specific SDK language:

```sh
# JS only
npx vitest run --reporter=verbose --testNamePattern "azure-sdk-for-js"

# Java only
npx vitest run --reporter=verbose --testNamePattern "azure-sdk-for-java"
```

### Filtering by test name

Combine language and test name patterns:

```sh
# Only "inits from local" for JS
npx vitest run --reporter=verbose --testNamePattern "azure-sdk-for-js.*inits from local"
```

## Debugging in VS Code

1. Add a launch configuration to `.vscode/launch.json` at the workspace root:

   ```jsonc
   {
     "type": "node",
     "request": "launch",
     "name": "Debug vitest - JS inits from local",
     "program": "${workspaceFolder}/eng/common-tests/tsp-client/node_modules/vitest/vitest.mjs",
     "args": [
       "run",
       "--reporter=verbose",
       "--testNamePattern",
       "azure-sdk-for-js.*inits from local",
     ],
     "cwd": "${workspaceFolder}/eng/common-tests/tsp-client",
     "console": "integratedTerminal",
     "skipFiles": ["<node_internals>/**"],
   }
   ```

2. Set a breakpoint in [test/tsp-client.test.js](test/tsp-client.test.js).
3. Open **Run and Debug** (`Ctrl+Shift+D`) and select the configuration.
4. Press **F5** to start debugging.

## Troubleshooting

### Locked worktrees from previous failed runs

If a test run is interrupted, git worktrees may be left in a locked state, causing subsequent runs to fail with:

```
fatal: cannot remove a locked working tree
```

To fix, list and force-remove stale worktrees for the affected SDK repo:

```sh
git -C /path/to/azure-sdk-for-js worktree list
git -C /path/to/azure-sdk-for-js worktree remove /path/to/stale-worktree --force
```

### Hook timeouts

The `beforeAll` hook creates git worktrees, which can take a long time for large repos. The timeout is configured in [vitest.config.js](vitest.config.js) (`hookTimeout: 240_000`). If you experience timeouts, check for locked worktrees first (see above), as those cause `git worktree add` to hang.
