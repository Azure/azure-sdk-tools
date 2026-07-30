# Rust SDK APIView Exporter - Design Document

## Overview

The design center moved to `Azure/azure-sdk-for-rust/eng/tools/generate_api`.
That Rust tool now owns extraction, shaping, and APIView rendering for current
Rust SDK reviews. It emits the full APIView tree-style `CodeFile` JSON directly,
with `ParserVersion` set from the tool version (`2.0.0` today, and `>= 2.0.0`
for future compatible revisions).

This TypeScript project remains in place for **compatibility**:

- **new inputs**: pass through APIView token files whose `ParserVersion >= 2.0.0`
- **legacy inputs**: convert older rustdoc JSON artifacts into APIView `CodeFile`
  JSON using the existing conversion pipeline

## Current architecture

The authoritative pipeline is now:

```
Rust crate source
→ cargo rustdoc JSON
→ generate_api extraction/model building
→ generate_api APIView rendering
→ APIView token file (ParserVersion >= 2.0.0)
→ APIView upload
```

At a high level, `generate_api` does the following in the Rust repository:

1. loads workspace metadata for the requested crate
2. runs `cargo rustdoc -Z unstable-options --output-format json`
3. extracts a repository-specific API model from rustdoc JSON
4. renders either `API.md` or APIView `CodeFile` JSON
5. stages the APIView output for the existing pack/create-apireview flow

For APIView output, the Rust implementation already writes the schema fields
APIView expects, including:

- `PackageName`
- `PackageVersion`
- `ParserVersion`
- `Language`
- `ReviewLines`

It also owns tokenization, stable `LineId` generation, nested `Children`,
documentation comment tokens, and validation such as duplicate-`LineId`
rejection.

## Current pipeline integration

The Rust repo's current API review caller chain remains:

1. `eng/pipelines/pr.yml` or `eng/pipelines/pullrequest.yml`
2. `eng/pipelines/templates/stages/archetype-sdk-client.yml`
3. `eng/pipelines/templates/jobs/ci.yml`
4. `eng/pipelines/templates/jobs/pack.yml`
5. `eng/scripts/Pack-Crates.ps1`

`Pack-Crates.ps1` runs `generate_api --format apiview`, copies the produced
`apiview.json` into the staged artifact for the shared `create-apireview` step,
which uploads that token file directly. `Create-APIReview.ps1` already has a
separate upload path for pre-generated review token files.

For manual uploads, rename `apiview.json` to `<package>.rust.json` before
sending it to APIView.

## Role of this parser now

This repository is no longer the primary renderer for current Rust SDK APIView
reviews. Its role is to preserve compatibility with older artifacts and with any
APIView invocation path that still calls the parser.

The entry point behavior is intentionally simple:

```
Input JSON
├─ ParserVersion >= 2.0.0
│  └─ write unchanged
└─ otherwise
   └─ run legacy rustdoc → APIView conversion
```

That keeps the migration safe for already-generated token files while preserving
the ability to regenerate output from older stored inputs.

## Legacy design retained for older artifacts

Before `generate_api`, the Rust repo used `eng/tools/generate_api_report` to
produce cleaned rustdoc JSON such as `<package>.rust.json`. This project then:

1. parsed rustdoc JSON
2. recursively processed Rust items and re-exports
3. generated APIView review lines and tokens
4. emitted tree-style APIView JSON with this parser's own `ParserVersion`

That legacy path is still the one to use for older files that:

- have no `ParserVersion`
- have an invalid `ParserVersion`
- have `ParserVersion < 2.0.0`

### Legacy processing model

```
Input (legacy rustdoc JSON)
→ Parsing
→ Recursive item processing
→ Review line generation
→ APIView JSON output
```

Key legacy components:

1. **`main.ts`**: CLI entry point, pass-through detection, JSON I/O
2. **`processItem.ts`**: recursive dispatcher for Rust constructs
3. **`process-items/*`**: specialized handlers for modules, structs, enums,
   traits, functions, `use`, constants, and related items
4. **`rustdoc-types/`**: TypeScript bindings derived from Rust rustdoc models

## Legacy compatibility constraints

The older flow depended on tight version alignment between the selected nightly
toolchain, rustdoc JSON `format_version`, the vendored `rustdoc-types`, and the
parser's conversion logic. That matters only when reprocessing older rustdoc JSON
artifacts; it is no longer the design center for current Azure SDK for Rust API
reviews.

## Practical guidance

- For **current** Rust SDK work, use `eng/tools/generate_api --format apiview`
  in `azure-sdk-for-rust` and send that output directly to APIView.
- For **older stored rustdoc JSON files**, keep using this parser so they can be
  converted into APIView `CodeFile` JSON.