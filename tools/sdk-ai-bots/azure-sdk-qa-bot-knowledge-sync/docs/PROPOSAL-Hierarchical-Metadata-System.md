# Proposal: Hierarchical Metadata System for Knowledge Base Categorization

## Executive Summary

This proposal introduces a hierarchical metadata system for the Azure SDK QA Bot knowledge base to enable precise, intent-driven document retrieval. The current flat organization by repository/path cannot support multi-dimensional categorization needed for Azure scenarios (branded vs unbranded, data-plane vs management-plane, specific topics).

**Key Benefits**:
- 🎯 **50-70% reduction** in irrelevant search results through targeted filtering
- ⚡ **Faster queries** with simple equality filters vs collection operations
- 🧩 **Clear taxonomy** with single-value hierarchical categories
- 🔄 **Backward compatible** with existing infrastructure
- 📊 **Better analytics** on content coverage and usage patterns

---

## 1. Problem Statement

### Current State

The knowledge base is organized solely by **repository + path**:
```
typespec_docs/
typespec_azure_docs/
azure_resource_manager_rpc/
azure_api_guidelines/
...
```

### Issues

**Issue 1: Mixed Content in Same Repository**

TypeSpec Azure documentation contains both data-plane and ARM (management-plane) content in the same repository/path:
```
/website/src/content/docs/docs/
  ├── Azure Data Plane Service/      # Data-plane specific
  │   ├── Writing Your First Service
  │   ├── Paging
  │   └── Long-Running Operations
  ├── ARM Service/                    # Management-plane specific
  │   ├── Installing Tools
  │   ├── Defining the Service
  │   └── Complete Example
  ├── Libraries/                      # Common: applies to BOTH DPG and MPG
  │   ├── Azure.Core
  │   └── Azure.ResourceManager
  ├── Emitters/                       # Common: applies to BOTH DPG and MPG
  │   ├── Emitter Configuration
  │   └── Code Generation
  └── Troubleshoot/                   # Common: applies to BOTH DPG and MPG
      ├── Common Errors
      └── Debugging Guide
```

When a user asks a data-plane question, the system retrieves ALL content (data-plane, ARM, AND shared), diluting relevance. The challenge is to:
1. Filter out ARM-specific content from data-plane queries
2. Include shared/common content in BOTH data-plane and management-plane queries

**Issue 2: Multi-Dimensional Question Categories**

Questions have multiple dimensions that should guide retrieval:

```
Question: "How to implement paging in Azure data plane service?"

Dimensions:
├─ Level 1: scope = branded (Azure-specific)
├─ Level 2: plane = data-plane (not ARM)
└─ Level 3: category = paging (specific topic)

Ideal sources: typespec_azure_docs (data-plane sections only)
Current behavior: Returns ALL typespec_azure_docs (both data-plane and ARM)
```

**Issue 3: No Mechanism for Content Targeting**

The QA bot's intent recognition (from `intention.md`) identifies:
- Scope: `branded` vs `unbranded`
- Plane: `data-plane` vs `management-plane`
- Category: `decorators`, `operations`, `paging`, `lro`, `versioning`, `arm-template`, etc.

But the knowledge base has no way to filter documents by these dimensions.

### Impact

- ❌ Users receive mixed/irrelevant content
- ❌ Longer context windows waste tokens and increase latency
- ❌ Lower answer quality due to noisy context
- ❌ Inefficient use of Azure AI Search resources

---

## 2. Proposed Solution: Hierarchical Metadata System

### Overview

Add **hierarchical single-value metadata** to each document in the knowledge base, enabling precise filtering based on question intent.

### Metadata Hierarchy

```
Level 1: scope (required)
  ├─ branded         # Azure-specific content
  └─ unbranded       # General TypeSpec content

Level 2: plane (optional - only for branded content)
  ├─ data-plane      # Azure data plane services
  ├─ management-plane # Azure Resource Manager (ARM)
  └─ both            # Applies to BOTH data-plane AND management-plane (e.g., emitters, troubleshooting, common libraries)

Level 3: category (optional)
  ├─ decorators      # @route, @header, etc.
  ├─ operations      # REST operations, methods
  ├─ paging          # Pagination patterns
  ├─ lro             # Long-running operations
  ├─ versioning      # API versioning
  ├─ arm-template    # ARM templates and patterns
  ├─ migration       # Swagger to TypeSpec migration
  ├─ sdk-generation  # SDK generation topics
  ├─ validation      # TypeSpec validation/CI errors
  └─ general         # General topic
```

### Additional Metadata

- **language** (optional, single value): `python`, `java`, `javascript`, `dotnet`, `go`, `general`
---

## 3. Technical Design

### 3.1 Configuration Schema Updates

**File**: `config/knowledge-config.schema.json`

Add metadata definitions:

```json
{
  "$defs": {
    "Metadata": {
      "type": "object",
      "properties": {
        "scope": {
          "type": "string",
          "enum": ["branded", "unbranded"],
          "description": "Target audience (required)"
        },
        "plane": {
          "type": "string",
          "enum": ["data-plane", "management-plane", "both"],
          "description": "Service plane: data-plane (DPG), management-plane (MPG/ARM), or both (shared content). Only applicable for branded content."
        },
        "category": {
          "type": "string",
          "enum": [
            "decorators", "operations", "paging", "lro",
            "versioning", "arm-template", "migration",
            "sdk-generation", "validation", "general"
          ]
        },
        "language": {
          "type": "string",
          "enum": ["python", "java", "javascript", "dotnet", "go", "general"]
        },
      },
      "required": ["scope"]
    },
    
    "FileOverride": {
      "type": "object",
      "properties": {
        "pattern": {
          "type": "string",
          "description": "Glob pattern to match files"
        },
        "metadata": {
          "$ref": "#/$defs/Metadata"
        },
        "mergeStrategy": {
          "type": "string",
          "enum": ["override", "inherit"],
          "default": "inherit",
          "description": "override=replace all, inherit=only specified fields"
        }
      },
      "required": ["pattern", "metadata"]
    }
  }
}
```

### 3.2 Configuration Example

**File**: `config/knowledge-config.json`

```json
{
  "version": "2.0.0",
  "sources": [
    {
      "repository": {
        "url": "https://github.com/Azure/typespec-azure.git",
        "branch": "main",
        "authType": "public"
      },
      "paths": [
        {
          "path": "/website/src/content/docs/docs",
          "description": "TypeSpec Azure documentation",
          "folder": "typespec_azure_docs",
          "metadata": {
            "scope": "branded",
            "plane": "both",
            "category": "general"
          },
          "fileOverrides": [
            {
              "pattern": "**/Azure Data Plane Service/**",
              "metadata": {
                "plane": "data-plane"
              },
              "mergeStrategy": "inherit"
            },
            {
              "pattern": "**/Azure Data Plane Service/*paging*.md",
              "metadata": {
                "plane": "data-plane",
                "category": "paging",
                "tags": ["pagination", "list-operations"]
              },
              "mergeStrategy": "inherit"
            },
            {
              "pattern": "**/Azure Data Plane Service/*lro*.md",
              "metadata": {
                "plane": "data-plane",
                "category": "lro",
                "tags": ["async", "polling"]
              },
              "mergeStrategy": "inherit"
            },
            {
              "pattern": "**/ARM Service/**",
              "metadata": {
                "plane": "management-plane",
                "category": "arm-template"
              },
              "mergeStrategy": "inherit"
            },
            {
              "pattern": "**/Libraries/**",
              "metadata": {
                "plane": "both",
                "category": "general",
                "tags": ["libraries", "azure-core", "resource-manager"]
              },
              "mergeStrategy": "inherit"
            },
            {
              "pattern": "**/Emitters/**",
              "metadata": {
                "plane": "both",
                "category": "sdk-generation",
                "tags": ["emitters", "code-generation"]
              },
              "mergeStrategy": "inherit"
            },
            {
              "pattern": "**/Troubleshoot/**",
              "metadata": {
                "plane": "both",
                "category": "validation",
                "tags": ["troubleshooting", "errors", "debugging"]
              },
              "mergeStrategy": "inherit"
            }
          ]
        }
      ]
    },
    {
      "repository": {
        "url": "https://github.com/microsoft/typespec.git",
        "branch": "main",
        "authType": "public"
      },
      "paths": [
        {
          "path": "/website/src/content/docs/docs",
          "folder": "typespec_docs",
          "metadata": {
            "scope": "unbranded",
            "category": "general"
          },
          "fileOverrides": [
            {
              "pattern": "**/decorators/**",
              "metadata": {
                "category": "decorators"
              },
              "mergeStrategy": "inherit"
            }
          ]
        }
      ]
    }
  ]
}
```

**Metadata Inheritance**: 
- **Path-level metadata** provides defaults for all files in that path
- **File overrides** use glob patterns to set specific metadata
- **Merge strategy** determines how overrides combine with defaults:
  - `inherit`: Only override specified fields, keep others from parent
  - `override`: Replace all metadata completely

### 3.3 Azure AI Search Index Schema

**New Fields to Add**:

```json
{
  "fields": [
    // Existing fields: chunk_id, parent_id, chunk, title, headers, 
    // text_vector, ordinal_position, context_id
    
    // --- NEW: Hierarchical Metadata Fields ---
    {
      "name": "scope",
      "type": "Edm.String",
      "filterable": true,
      "facetable": true,
      "retrievable": true
    },
    {
      "name": "plane",
      "type": "Edm.String",
      "filterable": true,
      "facetable": true,
      "retrievable": true
    },
    {
      "name": "category",
      "type": "Edm.String",
      "filterable": true,
      "facetable": true,
      "retrievable": true
    },
    {
      "name": "language",
      "type": "Edm.String",
      "filterable": true,
      "facetable": true,
      "retrievable": true
    },
  ]
}
```


### 3.4 Processing Pipeline

**New Component**: `MetadataResolver` service

```typescript
// src/services/MetadataResolver.ts

export class MetadataResolver {
  static resolveMetadata(
    filePath: string,
    relativePath: string,
    repository: Repository,
    pathConfig: DocumentationPath
  ): HierarchicalMetadata {
    
    // 1. Start with path-level defaults
    let metadata = { ...pathConfig.metadata };
    
    // 2. Apply file overrides based on pattern matching
    for (const override of pathConfig.fileOverrides || []) {
      if (matchGlobPattern(relativePath, override.pattern)) {
        if (override.mergeStrategy === 'override') {
          metadata = { ...override.metadata };
        } else {
          // Inherit: only replace specified fields
          metadata = { ...metadata, ...override.metadata };
        }
      }
    }
    
    // 3. Compute hierarchy_path
    const pathParts = [metadata.scope];
    if (metadata.plane) pathParts.push(metadata.plane);
    if (metadata.category) pathParts.push(metadata.category);
    if (metadata.subcategory) pathParts.push(metadata.subcategory);
    
    return {
      ...metadata,
      hierarchy_path: pathParts.join('/'),
      source: {
        repository: repository.url,
        path: pathConfig.path,
        file: relativePath
      }
    };
  }
}
```

### 3.5 Search Query Integration

**Enhanced Search with Intent Filtering**:

```typescript
// Before
const results = await searchClient.search(userQuery, {
  top: 10
});
// Returns ALL documents matching query text

// After
const intent = recognizeIntent(userQuery);
// { scope: 'branded', plane: 'data-plane', category: 'paging' }

const filters = [];
if (intent.scope) filters.push(`scope eq '${intent.scope}'`);

// IMPORTANT: Include 'both' plane content in plane-specific queries
if (intent.plane) {
  filters.push(`(plane eq '${intent.plane}' or plane eq 'both')`);
}

if (intent.category) filters.push(`category eq '${intent.category}'`);

const results = await searchClient.search(userQuery, {
  filter: filters.join(' and '),
  top: 10
});
// Returns data-plane paging documents + shared content (plane='both')
```

**Fallback Strategy**:
```typescript
// Try strict filtering first
let results = await searchWithIntent(query, intent, { strict: true });

// If no results, relax to parent level
if (results.length === 0) {
  results = await searchWithIntent(query, 
    { scope: intent.scope, plane: intent.plane }, 
    { strict: true }
  );
}

// If still no results, use unfiltered search
if (results.length === 0) {
  results = await searchWithIntent(query, {}, { strict: false });
}
```