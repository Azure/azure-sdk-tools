# OAV Report Generation

* [Related Issue](https://github.com/Azure/azure-sdk-tools/issues/2268)

The goal of this script is to generate a useful summary from the `oav` live validation output.

# Installation and Build

- Install TypeScript Globally

```
npm install
tsc generate_report.ts --outDir build/
```

## Run

```
node ./build/generate_report.js html -p ./oav-output/ -s <path-to-azure-rest-api-specs-repo>/specification/cosmos-db/data-plane/Microsoft.Tables/preview/2019-02-02/table.json
```