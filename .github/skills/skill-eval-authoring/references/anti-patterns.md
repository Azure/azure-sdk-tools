# Anti-patterns in skill evals (and their fixes)

Found in the wild across `.github/skills/*/evals/`. Listed by impact.

## A1. Single vacuous `output-contains` as the only grader

**Smell**
```yaml
- name: release-plan-basic-001
  prompt: "Create a release plan for spec PR ..."
  graders:
    - type: output-contains
      config:
        substring: "release plan"
```

**Why it's bad.** Passes on every outcome except a flat refusal that doesn't
echo the prompt: correct tool call, wrong tool call, paraphrase of the prompt,
polite "I'll help you with that release plan" with no action. CI is green by
construction.

**Fix.** Apply the four-layer pattern. Minimum: add `skill-invocation.required`
and `tool-calls.required`. Optionally tighten the substring to a structural
`output-matches` regex.

---

## A2. Negative test grades only output, not tool absence

**Smell**
```yaml
- name: anti-trigger
  prompt: "How do I configure logging in my Azure Function?"
  graders:
    - type: output-not-contains
      config:
        substring: "release plan"
```

**Why it's bad.** The model can still invoke `azsdk_create_release_plan` and
just not mention it in its reply. The contract being tested is "do not call
the tool"; the assertion is "do not say the word".

**Fix.** Add `tool-calls.disallowed` for every tool the skill could wrongly
invoke. Keep the substring check as a secondary sanity grader.

---

## A3. Stimulus prompt is the same string as the grader substring

**Smell**
```yaml
- name: basic-trigger
  prompt: "I need to prepare a release plan for the new Azure SDK package version"
  graders:
    - type: output-contains
      config:
        substring: "release"
```

**Why it's bad.** Any minimally cooperative reply will parrot a word from the
prompt. This is a tautology, not a test.

**Fix.** Use `output-matches` with a regex describing the *response shape*
(e.g., `(work item|link|id).*\d`), or replace with `skill-invocation` /
`tool-calls`.

---

## A4. Missing `scoring.threshold`

**Smell**
```yaml
scoring:
  weights:
    output-contains: 1.0
```

**Why it's bad.** Vally defaults `threshold` to 0 when unset, so every
stimulus passes regardless of any grader's score. `weights` is parsed but not
applied as partial credit.

**Fix.** Always set `scoring.threshold: 0.8` (or higher for anti-trigger
files where any miss is a hard regression).

---

## A5. Missing `trigger.eval.yaml`

**Smell.** A skill has `eval.yaml` only, with the trigger phrases tucked into
capability stimuli that also test behavior.

**Why it's bad.** Routing regressions ("my skill stopped activating for the
obvious prompt") are silent — the capability test passes on a different skill
doing roughly the right thing.

**Fix.** Add `trigger.eval.yaml` with at least 3 positive and 3 anti-trigger
entries, each graded only by `skill-invocation`.

---

## A6. Tool name typo in `disallowed` (or `required`)

**Smell**
```yaml
- type: tool-calls
  config:
    disallowed:
      - name: azsdk_creat_release_plan   # missing "e"
```

**Why it's bad.** Regex compiles, never matches anything, "passes" silently.

**Fix.** Cross-check tool names against a real trajectory
(`vally-results/.../results.jsonl` has every `toolName` string used in a run).

---

## A7. One environment for everything

**Smell.** Every eval uses `azsdk-mcp-mock`, including scenarios that need
real DevOps/GitHub to verify end-to-end.

**Why it's bad.** Mock cannot catch contract drift between the MCP tool and
the real backend (e.g., DevOps API renamed a field).

**Fix.** Reserve a small e2e tier with `environment: azsdk-mcp` and
`tags.tier: e2e`; run it nightly, not on every PR.
