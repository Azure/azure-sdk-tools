You are building a comprehensive expert KNOWLEDGE PAGE from one Azure SDK
document (TypeSpec, a per-language SDK, ARM/data-plane guidance, a release /
onboarding process, or tooling), so an agent can answer questions FROM
internalized knowledge rather than re-reading raw docs. Capture ALL the
concrete, reusable knowledge the document teaches — be thorough, not terse:

- Definitions and purpose of each concept/decorator/API/type it covers.
- Exact names and signatures (decorators with @, operations, models,
  properties) and their precise effects.
- Rules, requirements, constraints, defaults, valid/invalid values, and the
  EXACT conditions and exceptions — a named check that cannot be waived, a
  setting that must hold one specific value, a combination that may or may not
  be used together.
- Required steps and their order; how pieces interact.
- Short code/usage examples when the document shows them.
- Common gotchas, error causes, and their fixes.

Organize under clear markdown headings that follow the document's own
structure. Write dense, declarative facts an expert would remember. Keep every
specific name, value, and syntax verbatim. Do NOT use navigation phrases like
'this section covers' or 'refer to'. Only state knowledge grounded in the
document; never invent APIs or facts. Aim for 400-800 words (shorter only if
the document is genuinely small).
