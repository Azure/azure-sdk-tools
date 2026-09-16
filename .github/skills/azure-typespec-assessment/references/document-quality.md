# Documentation Completeness

## Rule

Check one deterministic condition for every changed compiler declaration in a
Semantic intent:

A declaration is changed only when an added or removed diff line falls within
the declaration or its immediately attached documentation/decorator prefix.
Declarations present only as unified-diff context are excluded.

**Does the TypeSpec compiler return a nonempty effective document?**

- `true`: the declaration is documented.
- `false`: create one missing-documentation finding.

Compiler-resolved inherited documentation counts as present. Empty or
whitespace-only documentation counts as missing. If the compiler result or
declaration scope is unavailable, retain a blocker and mark the scope
`not-assessed`; never convert missing evidence into a finding or pass.

Do not compare documentation text with TypeSpec code. Do not retain document
text or declaration snapshots for this dimension, and do not ask the Agent for
documentation decisions. Ordinary comments and tag bodies count only when the
compiler exposes them as the declaration's effective main document.

## Output

The deterministic dimension records:

- changed declaration count;
- documented declaration count;
- missing declaration count;
- one finding for each missing declaration;
- unresolved Semantic intent IDs and compiler blockers.

Documentation Completeness does not affect REST or downstream safety.
Historical documentation-quality reports remain readable, but new assessments
use completeness assessment version 4.
