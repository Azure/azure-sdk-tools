# Skills vs MCP Tools Decision Framework

## Overview

This document provides criteria for deciding when to use Agent Skills vs MCP Tools for Azure SDK tooling.

## Decision Matrix

### Keep as MCP Tool When:

| Criteria | Rationale |
|----------|-----------|
| **External API calls** | Tools handle authentication, retries, rate limiting |
| **State mutations** | Tools provide transactional guarantees |
| **Real-time data** | Tools fetch current state from services |
| **Idempotency matters** | Tools can implement proper idempotency |
| **Validation required** | Tools can enforce business rules in code |

### Create a Skill When:

| Criteria | Rationale |
|----------|-----------|
| **Workflow guidance** | Skills document multi-step processes |
| **Decision trees** | Skills help users choose between options |
| **Best practices** | Skills capture tribal knowledge |
| **Templates/patterns** | Skills provide reusable content |
| **Frequently updated** | Skills are Markdown = no rebuild needed |

### Hybrid Approach (Recommended):

**Skill orchestrates MCP Tools**
- Skill provides: When to use, decision flow, error handling guidance
- MCP Tool provides: Actual execution, API calls, validation

## Consolidation Guidelines

### Consolidate into ONE Skill when:
- Same user intent with different parameters
- Same domain (e.g., all storage operations)
- Combined content < 5000 tokens

### Split into SEPARATE Skills when:
- Different user intents (create vs deploy)
- Trigger phrases conflict
- Content exceeds 5000 tokens even with references/

## Current Tool → Skill Mapping

| Skill Domain | MCP Tools Guided | Status |
|--------------|------------------|--------|
| `typespec-new-project` | `azsdk_typespec_init_project` | ✅ Created |
| `sdk-release-workflow` | 9 release plan tools | ❌ Proposed |
| `typespec-migration` | `azsdk_convert_swagger_to_typespec`, validation | ❌ Proposed |
| `sdk-package-development` | build, test, generate, check | ❌ Proposed |
| `environment-setup` | `azsdk_verify_setup` | ❌ Proposed |

## Token Budget

| File Type | Soft Limit | Hard Limit | Action if Exceeded |
|-----------|------------|------------|-------------------|
| SKILL.md (description only) | 100 tokens | 500 tokens | Shorten description |
| SKILL.md (full content) | 500 tokens | 5000 tokens | Move to references/ |
| references/*.md | 1000 tokens | 2000 tokens | Split into multiple files |

**Estimation**: ~4 characters = 1 token

## Validation Checklist

Before merging a new Skill:

- [ ] Description contains trigger phrases (USE FOR: ...)
- [ ] Content under 500 token soft limit (or uses references/)
- [ ] Related MCP tools documented
- [ ] At least 3 test prompts defined
- [ ] Manual workflow test passes
