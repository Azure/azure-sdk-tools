# Rust SDK APIView Exporter - Design Document

## Overview

The Rust SDK APIView Exporter transforms Rustdoc JSON documentation into structured APIView JSON for Azure SDK API reviews. This document outlines the architecture, components, and key design decisions of the project.

## Goals

- Facilitate APIView reviews for Azure SDK for Rust repository.
- Automate conversion from Rustdoc JSON to structured APIView JSON.
- Ensure accurate representation of Rust constructs and their relationships.
- Provide clear, navigable, and consistent APIView JSON output.

## High-Level Architecture

The tool follows a multi-stage pipeline to process Rustdoc JSON:

```
Input (Rustdoc JSON) 
→ Parsing 
→ Recursive Item Processing 
→ Review Line Generation 
→ (APIView JSON) Output
```

## Core Components

1. **Entry Point (`main.ts`)**: Handles CLI arguments, JSON I/O, process orchestration, and validation.
2. **Recursive Processing**: Central dispatcher (`processItem.ts`) delegates to specialized processors for Rust constructs, preserving hierarchical relationships.
3. **Type System**: Rustdoc types from rust-lang repository, in `rustdoc-types/`, converted to TypeScript interfaces via [typeshare](https://github.com/1password/typeshare).
4. **Output Generation**: Produces structured APIView JSON with syntax highlighting and navigation metadata.

## Item Processing

The system processes these Rust constructs:
- Modules, Functions, Structs, Enums, Traits, Type Aliases, Constants, Statics, Use Declarations, Macros, Associated Types and Constants.

## Data Flow

1. **Input**: Read and validate Rustdoc JSON.
2. **Root Processing**: Process crate root, maintain hierarchy.
3. **Re-export Handling**: Resolve internal/external re-exports that can be modules/individual items.
4. **Output**: Generate structured APIView JSON.

## Recursive Processing

- Rust constructs are processed according to their natural containment hierarchy.
- Central dispatcher (`processItem.ts`) recursively delegates to specialized handlers, processing depth follows the natural nesting of items.

**Dispatch Logic**
   ```
   *processItem*
      ├─► processModule
      │      ├─► *processItem* (for each child)
      │      └─► processUse (for re-exports)
      ├─► processStruct
      │      ├─► processStructField
      │      └─► processImpl
      └─► processEnum
      .      └─► processVariant
      .      .
      .      .
   ```

## Additional Design Considerations

- **Type Bridging**: Cross-language type safety via [typeshare](https://github.com/1password/typeshare) between the Rustdoc types and the TS types.
- **Semantic Tokens**: Context-aware syntax highlighting and navigation.
- **Trait Implementation Analysis**: Comprehensive handling of trait implementations, specialized handling for derived trait, logical grouping of related implementations.
- **Re-export Resolution**: Deterministic resolution of module/item re-exports to prevent duplication in rendering and maintain correct hierarchy.
- **Sorting**: Sorts items within modules by type and name for consistent output. Sorts re-exports after the lines are generated to avoid redundancy.

## Rustdoc Types Component

Ensures type fidelity between Rustdoc and the derived TypeScript type definitions used in the parser implementation using [typeshare](https://github.com/1password/typeshare):

1. Vendored rustdoc-types crate for compatibility.
2. Automated typeshare/serde attribute injection.
3. Automated TypeScript interface generation.
4. Post-processing pipeline to address typeshare limitations.


### Architecture

```
┌───────────────────┐     ┌───────────────────┐     ┌───────────────────┐
│                   │     │                   │     │                   │
│  Rustdoc Types    │     │   Type Share      │     │  Post Processing  │
│  (Rust Crate)     ├────►│   Generation      ├────►│  (TypeScript)     │
│                   │     │                   │     │                   │
└───────────────────┘     └───────────────────┘     └───────────────────┘
        │                         │                          │
        ▼                         ▼                          ▼
┌───────────────┐         ┌──────────────┐          ┌──────────────────┐
│ Vendor Copy   │         │  Generated   │          │   Final Types    │
│ rustdoc-types │         │   .rs file   │          │   TypeScript     │
└───────────────┘         └──────────────┘          └──────────────────┘
```

### Rustdoc Types Data Flow

1. **Source Management**
   ```
   Vendored rustdoc-types
        │
        ▼
   add-typeshare.rs
        │
        ▼
   Generated lib-typeshare.rs
   ```

2. **Type Generation**
   ```
   lib-typeshare.rs
        │
        ▼
   TypeShare CLI
        │
        ▼
   typeshare-result.ts
   ```

3. **Post Processing**
   ```
   typeshare-result.ts
        │
        ▼
   post-processing.ts
        │
        ▼
   rustdoc-types.ts
   ```

## Future Steps

1. Update the get_api_report project in the azure-sdk-for-rust repo to show "paths" object in the JSON output as expected by the parser
2. Handle copyright/licensing details
3. Pipeline Integration at the tools repo, release the parser to the dev feed
4. CI setup at the rust repo
5. Rustdoc-types nightly sync job - Fails if it finds a mismatch of Format version between rustdoc output and the parser
6. Aesthetics - Support for more rendering classes
7. Expand template to cover edge cases in the next iteration
8. Generate API Views for the existing Rust projects from azure-sdk-for-rust repo
9. Rustdoc-types project shell script for easier regeneration process