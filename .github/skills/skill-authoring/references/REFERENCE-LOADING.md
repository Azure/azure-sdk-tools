# Reference Loading Behavior

This document explains how Agent Skills load reference files and best practices for structuring them efficiently.

## How References Load

### Just-In-Time (JIT) Loading

Reference files (`references/`, `scripts/`, `assets/`) are **NOT** loaded when a skill activates. They load **only when explicitly referenced** via a markdown link or file path in the skill instructions.

```markdown
<!-- This triggers a load -->
See [the guide](references/guide.md) for details.

<!-- This does NOT trigger a load -->
Documentation is available in the references folder.

<!-- This does NOT work - folder links don't load content -->
See [recipes](references/recipes/) for options.
```

### Link to Files, Not Folders

**Critical:** Always link to actual files, never directories. Folder references don't trigger content loading.

| ❌ Won't Load | ✅ Will Load |
|---------------|--------------|
| `[Languages](references/languages/)` | `[Languages](references/languages/README.md)` |
| `[Python](references/languages/python)` | `[Python](references/languages/python/README.md)` |
| `[Pipeline Stages](references/pipelines)` | `[Pipeline Stages](references/pipelines/README.md)` |

Use `README.md` as the entry point for folder-organized content.

### No Caching Between Requests

Per [agentskills Issue #97](https://github.com/agentskills/agentskills/issues/97):

> Reference files should be **fully loaded each time they are referenced**, regardless of whether they were previously read.

Implications:
- Don't assume the agent remembers previous file contents
- Write each reference as a **self-contained unit**
- Include necessary context within each file

### Whole File Loading

When a reference is loaded, the **entire file** loads - not just a section:

| Link | What Loads |
|------|------------|
| `[guide](references/guide.md)` | Entire guide.md |
| `[guide](references/guide.md#section-2)` | Entire guide.md (anchor is hint only) |

This means:
- Split large topics into separate files
- Keep each file < 1,000 tokens
- Don't create monolithic reference documents

## Token Efficiency Patterns

### Selective Loading with Recipes

```markdown
<!-- In azsdk-common-sdk-release/SKILL.md - agent loads only the relevant language guide -->
## SDK Release Workflow

Which language SDK are you releasing?
- [Python](references/languages/python/README.md) - PyPI package release
- [JavaScript](references/languages/javascript/README.md) - npm package release
- [Java](references/languages/java/README.md) - Maven Central release
- [.NET](references/languages/dotnet/README.md) - NuGet package release

<!-- Result: Only the chosen language guide loads (~300 tokens) -->
<!-- Not all four language guides (~1,200 tokens) -->
```

```markdown
<!-- In azsdk-common-pipeline-troubleshooting/SKILL.md - agent loads only the relevant failure type -->
## Pipeline Failure Triage

What kind of pipeline failure are you seeing?
- [Build Failures](references/failures/build/README.md) - Compilation and packaging errors
- [Test Failures](references/failures/test/README.md) - Unit, integration, or live test issues
- [ApiView Failures](references/failures/apiview/README.md) - API compatibility checks

<!-- Result: Only the relevant failure guide loads (~250 tokens) -->
<!-- Not all failure guides (~750 tokens) -->
```

## Self-Contained Reference Example

```markdown
# Pipeline Troubleshooting: Common CI Failures

Quick reference for Azure SDK CI pipeline issues (used by azsdk-common-pipeline-troubleshooting).

## Common Errors

| Error | Cause | Fix |
|-------|-------|-----|
| `ApiView check failed` | Breaking API changes detected | Review ApiView diff, update public API surface |
| `Credential test timeout` | Live test auth expired | Refresh service principal via `az login --service-principal` |
| `Package version conflict` | Version not bumped | Run version update per language convention |

## Troubleshooting Steps

1. Check pipeline logs: Navigate to the failed stage in Azure DevOps
2. Reproduce locally: Generate SDK and run tests for the target language
3. Validate API compatibility: Submit to ApiView for review

## Related

- [Release Plan Preparation](../azsdk-common-prepare-release-plan/references/README.md)
- [ApiView Feedback Resolution](../azsdk-common-apiview-feedback-resolution/references/README.md)
```

Note: This file works standalone without requiring other files to be loaded first.

## Skill Visibility Limits

From [GitHub Copilot CLI Issue #1130](https://github.com/github/copilot-cli/issues/1130):

- With many skills installed, not all appear in available skills list
- Only ~31 of 49 skills visible in one example due to token limits
- Hidden skills can still be invoked but aren't discoverable

**Best practices:**
- Keep `description` concise but keyword-rich
- Front-load trigger phrases for discoverability
- Don't rely on users seeing all skills

## Summary

| Behavior | Implication |
|----------|-------------|
| JIT loading | Only explicitly linked files load |
| No caching | Write self-contained references |
| Whole file loads | Split large content into small files |
| Token limits | Structure for selective loading |
