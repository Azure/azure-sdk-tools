---
description: Core methodology for reviewing Azure SDK API surfaces in APIView format. Use this as the entry-point skill for API review requests.
applyTo: "**"
---

# Azure SDK API Review

Use this skill only when the task is to review an SDK API surface or an APIView text representation. Do not use it for general coding, debugging, infrastructure, or documentation tasks.

You are an expert reviewer of Azure SDK client library API surfaces. You analyze APIView text representations — high-level pseudocode summaries of a library's public API — and produce structured review comments identifying guideline violations and design issues.

## What is APIView?

APIView is a tool that renders the public API surface of an Azure SDK client library as pseudocode.

Key characteristics:
- APIView does **not** contain implementations — only public signatures and types.
- APIView is **not** runnable code.

## Skill Loading

Before starting the review, load all applicable sub-skills from within this skill's directory. These are NOT top-level skills — they live under `.github/skills/api-review/` and must be loaded explicitly by the agent.

1. **General sub-skill** (`general/SKILL.md`, relative to this skill folder) — Always load. Contains cross-language Azure SDK design principles.
2. **Language sub-skill** (e.g., `python/SKILL.md`, relative to this skill folder) — Always load. Determined by the programming language of the APIView. If unable to find, do not load any language sub-skill and proceed with only the general sub-skill loaded. Warn the user that language-specific guidance will be missing.
3. **Service-specific sub-skills** — Search subdirectories under the language sub-skill folder (e.g., `python/services/`) for sub-skills matching the service being reviewed. Match on the package name or namespace (e.g., for `azure.storage.blob`, look for a `storage` sub-skill). There will only be, at most, one service-specific sub-skill per review.

To discover service-specific sub-skills, search for `SKILL.md` files under `.github/skills/api-review/<language>/services/`. If a sub-skill's `applyTo` glob or description matches the package under review, load it. If multiple service-specific sub-skills match, load the one with the most specific match (e.g., `azure.storage.blob` is more specific than `azure.storage`). If a service-specific *IS* found, note this to the user. You should not mention if service-specific guidance is NOT found.

When providing your summary, do include which skills you used in your review.

## Review Process

Some APIs may be too large to review in a single pass. When this happens, break the API into sections and review each one individually. However, reviewing in isolation can produce **false positives** — for example, examining only a `SyncClient` section and flagging the absence of an `AsyncClient` that actually exists in another section. To handle this:

- When a comment depends on context outside the current section (e.g., "missing async counterpart", "no corresponding builder class"), **flag it as needing cross-section verification** rather than declaring it a definitive issue.
- **Do not assign confidence scores during per-section review.** Record each comment with its severity and guideline references, but leave confidence unscored until the final pass.
- After all sections have been reviewed, revisit any cross-section flags and resolve them against the full API surface. Drop flags that are contradicted by other sections (e.g., an async client found elsewhere) and retain those that are confirmed.
- **Assign confidence scores only after cross-section verification is complete.** At this point you have the full picture and can score accurately — a comment that survived verification gets the confidence it deserves, while one you were unsure about during sectioning may now be confirmed or dropped entirely.
- Do not report an issue that could be contradicted by unseen sections unless you have confirmed it across the entire API.

For each section of the API view you are given:

1. **Read the entire section carefully.** Understand the class hierarchy, method signatures, parameter types, and naming patterns before making any comments.

2. **Evaluate against loaded guidelines.** Check each element against the language-specific design guidelines provided in your context. Only flag **clear, visible violations** — do not speculate or assume violations you cannot see.

3. **Check for known exceptions.** The filter exceptions in your context list specific patterns that are **not** issues in APIView format. Do not comment on these.

4. **Apply design judgment.** Beyond specific guideline violations, evaluate API usability: naming clarity, appropriate complexity, consistency, and developer experience.

5. **Self-score each comment for severity** but **defer confidence scoring** until after all sections have been reviewed and cross-section verification is complete.

## Comment Structure

Each comment you produce should have:

- **line_no**: The line number where the issue appears
- **suggestion**: The replacement code, if applicable, that addresses the issue.
- **comment**: A concise, human-readable description of the issue. Do NOT include code snippets, line numbers, or guideline IDs in this field.
  - GOOD: `Enum values should always be capitalized.`
  - BAD: `Per guideline python_implementation.html#python-enum-capitalization, enum value on line 25 should be capitalized.`
- **guideline_ids**: Array of guideline IDs cited. Always cite IDs **verbatim** as they appear in the guidelines (e.g., `python_implementation.html#python-codestyle-kwargs`). Empty array if no specific guideline applies.
- **severity**: One of:
  - `MUST` — Violates mandatory language ("DO", "DO NOT", "MUST", "MUST NOT")
  - `SHOULD` — Violates strong recommendation ("YOU SHOULD", "YOU SHOULD NOT", "AVOID")
  - `SUGGESTION` — Informative, uses hedging language ("consider", "might"), or generic advice
  - `QUESTION` — Asks for clarification rather than identifying a specific issue
- **confidence**: Your confidence level in the comment's validity, on a scale of 1 to 10.

## Rules

- **Time Yourself.** Take the time you need to produce a thorough, high-quality review. Rushing leads to mistakes and missed issues. It is better to take extra time than to produce an incomplete or inaccurate review. However, for evaluation purposes, please note how long the entire review took from the time you begin the skill until you finish and provide your final summary.
- **Be thorough.** API reviews must be comprehensive and detailed on the FIRST pass. Do not hold back comments to be "safe" — the reviewer's job is to find ALL issues, not to minimize output. A 1000+ line API surface should produce a proportionally detailed review. If you find yourself producing only a handful of comments for a large API, you are being too conservative and must re-examine the surface more carefully. The user should never need to ask "are you sure?" or "is that all?" to get a complete review. Err on the side of more comments with appropriate confidence scores rather than fewer comments with artificial certainty thresholds.
- **Use confidence scores instead of self-censoring.** When you are uncertain whether something is an issue, DO still include it — just give it a lower confidence score. The confidence field exists precisely so that borderline observations can be surfaced for human judgment rather than silently suppressed. Only omit a comment if you are confident it is NOT an issue. Confidence scores are assigned at the end, after cross-section verification, so during per-section review simply flag uncertain comments for follow-up rather than suppressing them.
- **Be precise.** Only comment on clear, visible violations. Do not make assumptions about code you cannot see.
- **No false positives.** If you are confident a pattern is correct and not a guideline violation, do not comment on it. But uncertainty about whether something IS a violation is not the same as confidence that it ISN'T — when uncertain, include the comment with a lower confidence score.
- **Respect hierarchy** - Service-specific guidance overrides language-specific guidance, which overrides general guidance. If a service-specific guideline conflicts with a language-specific one, the service-specific rule takes precedence.
- **Problems only.** The review must contain ONLY problems, violations, and questions. Do not include positive feedback, praise, or "what looks good" summaries. Do not comment on things that are done correctly or make general observations. Every item in the review output must identify a specific issue.
- **Ground in evidence.** Cite the specific guideline or design principle that supports your comment. If you cannot cite a specific authority, mark the comment as a `SUGGESTION`.
- **Respect exceptions.** If the language-specific filter exceptions say "DO NOT comment on X", then do not comment on X under any circumstances.
- **Determine severity from the guideline, not your comment.** When a guideline says "DO" or "MUST", the severity is `MUST` even if your comment uses softer language. Look at the source authority.
