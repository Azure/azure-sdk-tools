# OAV Report Generation

* [Related Issue](https://github.com/Azure/azure-sdk-tools/issues/2268)

The goal of this script is to generate a useful summary from the `oav` live validation output.

## Done

- [x] Get oav error output into a useful format by directly calling it against converted test-proxy recordings
- [x] Marry oav output and template expansion, generate report.

## ToDO

- [ ] Pivot of the raw oav output into something that can be easily iterated across in a UI template
- [ ] Template iteration, styling
- [ ] Move `generate_report.ts` into `oav` proper

## File Descriptions

 - `general_error_example.png`: What a "general" oav error looks like
 - `runtime_exception_example.png`: What a "runtime" oav exception looks like.
 - `generate_report.ts`: the actual workhorse of this directory. Compile and run directly from CLI.

# Build

- Install TypeScript Globally

```
npm install
tsc generate_report.ts --outDir build/
```

## Run

```
node ./build/generate_report.js html -p ./oav-output/ -s <path-to-azure-rest-api-specs-repo>/specification/cosmos-db/data-plane/Microsoft.Tables/preview/2019-02-02/table.json
```

## To inspect the objects

- Place a `debugger` in the appropriate place within `generate_report.ts`.
- chrome://inspect/
- Click `Open dedicated DevTools for Node`
- Add `--inspect` to the `node` invocation defined above, eg: `node --inspect ./build/generate_report.js html -p ./oav-output/ -s <path-to-azure-rest-api-specs-repo>/specification/cosmos-db/data-plane/Microsoft.Tables/preview/2019-02-02/table.json`
  - This is extremely useful for diving into object mappings during template expansion.

