# Markdown Assessment Template

```markdown
# TypeSpec Assessment

**Overall confidence:** `<high|medium|low>`

## Semantic Understanding

### Intent: 

<change summary>

**Confidence:** <high|medium|low>

**Transformation chain:**

1. <TypeSpec edit>
2. <resulting REST or client behavior>

**REST representation:** <summary>

#### `<operationId>`

- **HTTP path:** `<METHOD> <complete path>`
- **API versions:** `<versions>`
- **Parameters:** `<location, wire name, type, requiredness, default>`
- **Request payload:** `<content type and model, or none>`
- **Response payloads:** `<status, content type, model, relevant headers>`
- **Service behavior:** `<domain behavior>`
- **LRO:** `<pattern, polling, terminal states, final result, or no>`
- **Paging:** `<items, nextLink/marker, continuation behavior, or no>`
- **TypeSpec source:** `[path:Lx-Ly](path#Lx-Ly)`

## REST Breaking Changes

### <finding title>

- **Severity:** <high|medium|low>
- **Confidence:** <high|medium|low>
- **Summary:** <wire compatibility impact>
- **Evidence:** <AutoRest and source evidence>
- **TypeSpec source:** `[path:Lx-Ly](path#Lx-Ly)`

## REST-Compatible Downstream Breaking Changes

### <finding title>

- **Severity:** <high|medium|low>
- **Confidence:** <high|medium|low>
- **Summary:** <SDK or client impact while REST remains compatible>
- **Evidence:** <TCGC and source evidence>
- **TypeSpec source:** `[path:Lx-Ly](path#Lx-Ly)`

## Azure Compliance

`not-assessed` — Deferred from MVP.

## Document Quality

`not-assessed` — Deferred from MVP.

## Assessment Errors

<Compilation blockers, or “None”>

## Assessment Evidence

**Compared revisions:**

- **Baseline:** `<ref and commit>`
- **Head:** `<commit and working-tree state>`
- **Changed TypeSpec:** `[path:Lx-Ly](path#Lx-Ly)`

### Emitter Runs

| Project | Revision | Emitter | Output | Status | Evidence |
| --- | --- | --- | --- | --- | --- |
| `<project>` | baseline | `@azure-tools/typespec-autorest` (`autorest`) | OpenAPI | `<succeeded|failed>` | `<artifact directory and compile log>` |
| `<project>` | baseline | `@azure-tools/typespec-client-generator-core` (`tcgc`, generic) | SDK metadata | `<succeeded|failed>` | `<artifact directory and compile log>` |
| `<project>` | head | `@azure-tools/typespec-autorest` (`autorest`) | OpenAPI | `<succeeded|failed>` | `<artifact directory and compile log>` |
| `<project>` | head | `@azure-tools/typespec-client-generator-core` (`tcgc`, generic) | SDK metadata | `<succeeded|failed>` | `<artifact directory and compile log>` |

Repeat the four rows for every affected project.

### Artifact Evidence

- **AutoRest:** `<relevant base/head OpenAPI differences, or no wire difference>`
- **TCGC:** `<relevant base/head generic SDK metadata differences, or no client-shape difference>`
- **Source-only evidence:** `<scoped decorators or behavior not represented by the generic TCGC output, or none>`
```

Repeat the operation subsection for every affected operation and the finding
subsection for every finding. Use baseline links for deleted TypeSpec. State
“None detected” only when compilation and analysis completed successfully.
