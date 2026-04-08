# Cross-Language APIView Sample

A self-contained TypeSpec project that demonstrates cross-language client generation and
produces APIView artifacts for local cross-language review testing.

## Service definition

The sample defines a fictional **Azure Widget Service** in `main.tsp` using `Azure.Core`
resource patterns (CRUD + list + custom action). `client.tsp` adds per-language client
name customizations via TCGC (`@azure-tools/typespec-client-generator-core`).

| TypeSpec namespace | `Azure.Samples.CrossLanguage` |
|-|-|
| Python package | `azure-samples-crosslanguage` |
| JavaScript package | `@azure/samples-crosslanguage` |
| Java package | `com.azure.samples.crosslanguage` |
| .NET package | `Azure.Samples.CrossLanguage` |
| Go package | `azcrosslanguage` |

## Prerequisites

- [Node.js](https://nodejs.org/) ≥ 16
- [PowerShell](https://github.com/PowerShell/PowerShell) ≥ 7 (for the build script)

## Generate artifacts

```powershell
# From this directory
pwsh generate.ps1
```

What it does:

1. `npm install` — installs the TypeSpec compiler and emitters.
2. `tsp compile . --emit=@azure-tools/typespec-apiview` — produces
   `output/Azure.Samples.CrossLanguage.json` (the APIView token file).
3. `tsp compile . --emit=@azure-tools/typespec-metadata` — produces
   `output/typespec-metadata.json` (language package metadata). If the metadata emitter
   is not available it writes a hand-crafted stub so the zip is still usable.
4. Bundles both files into `output/Azure.Samples.CrossLanguage.zip`.

## Use the artifact for cross-language testing

### Upload the TypeSpec review

Upload `output/Azure.Samples.CrossLanguage.zip` to APIView as a new **TypeSpec** review
(either through the UI or via the DevOps artifact pipeline). APIView will:

- Create a TypeSpec review with `PackageName = "Azure.Samples.CrossLanguage"`.
- Set `CrossLanguagePackageId = "Azure.Samples.CrossLanguage"` on the review.
- Create a **Project** from the metadata, pre-populating expected packages for Python,
  JavaScript, Java, .NET, and Go.

### Upload per-language reviews

Generate and upload a review file for each language SDK. Each uploaded review should have
`CrossLanguagePackageId = "Azure.Samples.CrossLanguage"` set (the language emitters do
this automatically). APIView will then link the language review to the same project,
making them visible together in the cross-language view.

For manual testing you can upload any existing package review from the matching language
and patch its `CrossLanguagePackageId` value.

### Verify cross-language linking

1. Open the TypeSpec review in APIView.
2. The **Project** panel should show all expected language packages.
3. After uploading per-language reviews, each should appear linked under the same project.

## Project layout

```
cross-language-sample/
├── main.tsp          # Service definition (models + operations)
├── client.tsp        # TCGC per-language client customizations
├── tspconfig.yaml    # Compiler configuration + emitter options
├── package.json      # TypeSpec npm dependencies
├── generate.ps1      # Build + bundle script
└── output/           # Generated files (git-ignored)
    ├── Azure.Samples.CrossLanguage.json   # APIView token file
    ├── typespec-metadata.json             # Language metadata
    └── Azure.Samples.CrossLanguage.zip   # Upload-ready artifact
```

## Compile manually (without the script)

```bash
# Install dependencies
npm install

# Generate APIView token file
npx tsp compile . --emit=@azure-tools/typespec-apiview

# Generate language metadata
npx tsp compile . \
  --emit=@azure-tools/typespec-metadata \
  --option "@azure-tools/typespec-metadata.outputFile={project-root}/output/typespec-metadata.json" \
  --option "@azure-tools/typespec-metadata.format=json"
```
