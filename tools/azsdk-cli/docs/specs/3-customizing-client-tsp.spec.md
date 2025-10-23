# Spec: Customizing Client TypeSpec - [WIP]

## Table of Contents

- [Definitions](#definitions)
- [Background / Problem Statement](#background--problem-statement)
- [Goals and Exceptions/Limitations](#goals-and-exceptionslimitations)
- [Design Proposal](#design-proposal)
- [Alternatives Considered](#alternatives-considered)

---

## Definitions

_Terms used throughout this spec with precise meanings:_

- **<a id="typespec"></a>TypeSpec**: The specification language used to define Azure service APIs. SDK code is generated from TypeSpec specifications located in the azure-rest-api-specs repository.

- **<a id="typespec-customizations"></a>TypeSpec Customizations**: SDK-specific customizations made in the `client.tsp` file to control SDK generation. This term does not refer to modifications of the service API TypeSpec files.

- **<a id="code-customizations"></a>Code Customizations**: Hand-written modifications made directly to generated SDK code after generation. These customizations exist within the SDK language repositories and must be preserved across regeneration. Examples include adding convenience methods, custom error handling, or language-specific optimizations. Also known as "handwritten code" or "customization layer."

- **<a id="api-view"></a>API View**: A web-based tool for reviewing SDK APIs. It allows language architects and SDK team members to provide feedback on just the SDK APIs without needing to understand the underlying implementation.

---

## Background / Problem Statement

TypeSpec specs are written to describe service APIs. However, our team supports generating SDKs in no less than 7 languages, each differing in their idioms. The `client.tsp` file acts as a TypeSpec entry point for SDK emitters, applying language-specific customizations to the service spec that are then used by each respective SDK emitter.

Service teams cannot be expected to understand the nuances of each SDK language and how to best customize the generated code for each. Applying changes also requires understanding how to use [Azure.ClientGenerator.Core](https://azure.github.io/typespec-azure/docs/libraries/typespec-client-generator-core/reference/).

### Current State

Currently, feedback for adding client customizations to `client.tsp` can come in a few forms:

1. From reviews, e.g. API View feedback
2. From automated processes, e.g. code analyzers
3. From manually triggered processes, e.g. breaking changes analysis

**.NET**:
The .NET SDK has code analyzers that run during their SDK build (`dotnet build`) that identifies problematic code (e.g. class names that don't follow .NET naming conventions) and suggests fixes.
Many of these fixes are handled by adding customizations to `client.tsp` for .NET, such as renaming models with `@clientName`. Currently these fixes are manually applied after SDK builds fail.
_.NET has a clear flow for identifying and applying `client.tsp` customizations via: Emit SDK -> Build SDK -> Apply fixes -> Repeat._

**Go**:
The Go SDK has a [breaking changes](https://github.com/Azure/azure-sdk-for-go/blob/instruction/.github/prompts/go-sdk-breaking-changes-review.prompts.md) prompt that analyzes the generated SDK's changelog for specific patterns and maps those to `client.tsp` customizations where possible.
_We don't currently have a clear step in our process for identifying breaking changes, but the flow for Go is: Emit SDK -> Analyze changelog & apply fixes -> Repeat._

**All Languages**:
All language SDKs have their APIs reviewed in API View. Common feedback includes renaming models per language, or changing operations that appear in a client. We have documentation for customizing SDKs such as [renaming types](https://azure.github.io/typespec-azure/docs/howtos/generate-client-libraries/09renaming/), but recent (10/14/2025) testing shows that even with the current documentation, AI agents have difficulty applying these customizations correctly in a consistent manner: particularly when `scope` is required.

### Why This Matters

We should minimize the amount of time service teams spend on customizing their TypeSpec for each SDK language. Let them focus on what they are experts on - the service API - rather than figuring out how to apply SDK-specific feedback to their TypeSpec.

For automated processes like .NET, we can reduce the time spent in the outer loop of SDK build failures by proactively applying customizations to `client.tsp`.

For manual processes like API View reviews, we can improve the authoring experience to reduce the tedium and cognitive load of applying customizations manually.

---

## Goals and Exceptions/Limitations

### Goals

What are we trying to achieve with this design?

- [ ] Improve the service team experience for authoring `client.tsp` customizations
- [ ] Simplify the process of _how_ to apply `client.tsp` customizations for language teams

## Design Proposal

The goals can be broken down into 3 main areas for this design to address:

1. Make AI an expert on `client.tsp` customizations. The base expertise shouldn't have to be redefined by each language team.
2. Provide the infra in azsdk-cli to to utilize this expertise. Let language teams focus on _what_ they want to update rather than the _how_ as possible.
3. Update the `client.tsp` file, including from outside the azure-rest-api-specs repo.

Design TBD

## Alternatives Considered

### Alternative 1: Emit-Validate-Propose Loop

**Description:**
[Original design proposal](https://gist.github.com/chrisradek/9ab52a0a13faac6b794d32be87c26785)
A CLI command/MCP tool that takes a typespec project path and a package path (emitted SDK path) as input. This then runs a loop of: Emit SDK -> Validate SDK -> Propose customizations to `client.tsp` -> repeat.

**Pros:**

- Codifies the loop .NET already has into a reusable tool that enforces a sequence of steps
- Can be used by multiple languages - each language provides their implementation of the validate/propose steps

**Cons:**

- Too specific to .NET's workflow of emit, build, fix. Go better served with emit, validateAndFix.
- Not clear when AI should call the MCP tool, as each language may have a different process (e.g. .NET build vs Go changelog analysis)

**Why not chosen:**

Maps well to .NET, but less so for Go. Potentially too strict to be useful for other languages. Not yet clear on when to call this tool automatically outside of .NET.
