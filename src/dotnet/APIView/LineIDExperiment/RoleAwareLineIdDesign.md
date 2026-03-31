# Role-Aware LineId Creation Design

> Note from prior discussion:
> These generated lineIds are good enough for addressability and rough structural understanding by agent, but not
> rich enough to be treated as truly semantic names.

This document explains how the role-aware analyzer constructs IDs:
- Base heuristic ID (semantic-ish, may collide)
- Final ID (guaranteed unique using deterministic backoff ordinals)

Implementation source:
- src/dotnet/APIView/LineIDExperiment/analyze_token_coverage_roleaware.py
Related results:
- src/dotnet/APIView/LineIDExperiment/Analysis.RoleAware.md

## 1. High-Level Pipeline

For each line with a non-empty LineId in ReviewLines:

1. Collect line context
- line_text from tokens
- related_to_line
- owner_anchor from RelatedToLine or ancestor text chain

2. Assign a role (priority order)
- doc
- attribute
- signature
- other

3. Build a base fingerprint by role
- DOC|...
- ATTR|...
- SIG|...
- OTHER|...
- _unreachable when no payload survives

4. Apply role-specific ordinals
- doc => append |dN by owner
- attribute => append |nN by (owner, base)

5. Apply final collision backoff ordinal
- Any remaining duplicate final fingerprint gets |x1, |x2, ...

Result:
- Base fingerprint is interpretable and used for collision analysis
- Final fingerprint is deterministic and unique within the file

## 2. Role Classification Checks And Hints

## doc role

A line is doc if any token satisfies either:

- IsDocumentation == true
- token RenderClasses intersects:
  - comment
  - javadoc
  - doc
  - documentation

## attribute role

A line is attribute only if related_to_line is present and is_annotation_like(line_text) is true.

Annotation-like checks on left-trimmed text:
- regex ^[@#]\w
- regex ^\[[^\]]+\]
- starts with @

## signature role

A line is signature if either:

- token RenderClasses intersects:
  - methodname
  - membername
  - typename
  - parametertype
  - parametername
  - class
  - interface
  - enum
  - struct
  - function
  - constructor
  - returntype

or fallback:
- contains both ( and )
- and does not start with //, /*, *

## other role

Fallback when none of the above role checks match.

## 3. Owner Anchor Derivation

owner_anchor is used to scope IDs and ordinals.

Order:
1. If RelatedToLine is present: normalized RelatedToLine
2. Else: nearest non-empty normalized ancestor from chain
3. Else: root

Normalization:
- trim
- collapse whitespace to _

## 4. Base Fingerprint Construction

## doc base

DOC|{owner_anchor}|{doc_kind}

doc_kind buckets:
- doc_marker for exact /**, /*, ///, //, *
- doc_close for */
- doc_tag for lines starting with * @
- doc_body for lines starting with comment forms
- doc_other otherwise

## attribute base

ATTR|{owner_anchor}|{attribute_name}

attribute_name:
- strip leading sigils @, #, [
- first identifier-like token via regex [A-Za-z_][A-Za-z0-9_.-]*
- fallback attribute

## signature base

SIG|{owner_anchor}|{callable_name}|a{arity}|g{generic_arity}|t{type_shape}

Parts:
- callable_name
  - token class methodname/membername preferred
  - else identifier before first (
  - else last identifier
- arity
  - derived from first parameter list (...)
  - comma counting with nested < > and ( ) depth tracking
- generic_arity
  - first <...> comma count + 1 (approximate)
- type_shape
  - token values with parametertype/typename when available
  - otherwise token-kind profile fallback (k...)

## other base

OTHER|{owner_anchor}|{payload}

- payload from non-punctuation token values (Kind != 1)
- normalized with whitespace collapse
- if empty => _unreachable

## 5. Final Collision Backoff Ordinal

After role-based IDs are formed:

1. Count duplicates of current final fingerprints
2. For each still-colliding group in traversal order:
- append |x1, |x2, ...

Meaning:
- xN is a deterministic tie-breaker
- scoped to that exact colliding fingerprint in a file
- guarantees uniqueness but does not add semantics

## 6. Collision Class Labels Used In Reporting

Base collisions are labeled as:
- doc-comment for role doc
- decorator-annotation for role attribute
- overload-signature for role signature
- empty-or-whitespace for _unreachable
- other otherwise

## 7. Why Base And Final Are Both Kept

- Base fingerprints measure heuristic quality and explain collision patterns
- Final fingerprints provide operational uniqueness for LineId generation
