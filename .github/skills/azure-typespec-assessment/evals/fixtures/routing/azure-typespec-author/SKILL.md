---
name: azure-typespec-author
license: MIT
metadata:
  version: "1.0.0"
description: "Authors and modifies Azure TypeSpec (.tsp) API specifications. MUST BE USED FOR ALL TypeSpec changes regardless of complexity — even adding a single property or enum value requires this skill's validation workflow. USE FOR: any TypeSpec/tsp change — api versions (add, bump, preview, stable, promote), resources, operations, models, properties, decorators, visibility, constraints, breaking changes, LRO, suppressions, operationId, spread model. Covers both ARM resource-manager (Azure.ResourceManager) and data-plane (Azure.Core) services. DO NOT USE FOR: SDK generation, releasing SDK packages, or single MCP tool calls. INVOKES: azure-sdk-mcp:azsdk_typespec_generate_authoring_plan, azure-sdk-mcp:azsdk_run_typespec_validation."
compatibility: "azure-sdk-mcp server with azsdk_typespec_generate_authoring_plan and azsdk_run_typespec_validation tools"
---

# Azure TypeSpec Author routing fixture

This lightweight fixture preserves the production skill's routing metadata
without copying its large authoring eval assets into each assessment routing
trial.
