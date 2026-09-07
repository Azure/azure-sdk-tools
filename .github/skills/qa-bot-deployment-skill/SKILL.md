---
name: qa-bot-deployment-skill
description: "Transform multi-component chatbot deployments into consistent azd, Bicep, and pipeline workflows. WHEN: \"modernize chatbot deployment\", \"consolidate bot pipelines\", \"add environment provisioning\", \"design chatbot rollout and rollback\"."
license: MIT
metadata:
  author: Microsoft
  version: "1.0.0"
compatibility: "Requires Azure CLI, azd, Bicep, and the repository pipeline toolchain"
---

# QA Bot Deployment Transformation

Transform an existing chatbot deployment into a repeatable, auditable Azure
workflow while preserving the repository's established conventions.

## Workflow

1. **Inventory the system.** Identify deployable services, data jobs, Azure
   resources, identities, configuration stores, external integrations, current
   pipelines, and manual steps. Trace runtime and data dependencies before
   editing.
2. **Define one environment contract.** Keep subscription, tenant, regions,
   resource names, channel mappings, feature flags, approvals, and rollout
   policy in a versioned environment suite. Ensure local azd and CI load the
   same values.
3. **Separate responsibilities.** Provision infrastructure with layered Bicep;
   build and test components in CI; deploy artifacts or approved remote builds
   in CD; run migrations, indexing, and scheduled ingestion as explicit jobs.
4. **Model dependencies.** Order shared resources, identity, AI services,
   compute, integrations, application deployment, and data maintenance. Make
   every generated output consumed by a downstream component explicit.
5. **Secure identities.** Prefer workload identity federation and managed
   identity. Grant minimum control-plane and data-plane roles at the narrowest
   practical scope. Keep interactive Microsoft 365 consent outside unattended
   Azure deployment.
6. **Control state changes.** Make schema, index, routing, prompt, and knowledge
   migrations idempotent and observable. Do not rely on application startup to
   create durable production state.
7. **Add operational gates.** Include preview, approval, health checks,
   deployment verification, rollback, and production-only guards. Preserve a
   known-good revision or traffic route.
8. **Validate end to end.** Compile every Bicep layer, parse pipeline YAML,
   test hooks and applications, verify environment drift, and confirm data
   producer-to-consumer paths and identity permissions.

## Rules

- Reuse existing repository templates and naming conventions.
- Treat Git as the source of truth; document unavoidable external state.
- Keep environment-specific IDs out of reusable templates.
- Use immutable versions where supported and record the deployed version.
- Never silently overwrite user changes or live configuration without first
  reconciling the authoritative source.
- Keep changes focused and include tests proportional to deployment risk.

## Deliverables

Provide:

- current and target architecture;
- environment and identity contracts;
- component CI/CD and provisioning changes;
- migration, rollout, rollback, and validation steps;
- explicit remaining manual actions or unresolved placeholders.