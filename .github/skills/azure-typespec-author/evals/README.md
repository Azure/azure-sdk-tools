# Naming Skill Evals

This hermetic suite tests authoring-time naming plans, independently of the [live TypeSpec benchmarks](../evaluate/README.md). It mounts skills only: no MCP server, network source, SDK generator, or TypeSpec installation is required. Inline project facts represent confirmed intake; capability cases stop at the naming plan and do not edit TypeSpec.

Coverage includes routing and neighboring skills, C# ARM member/model rules, accepted exceptions, HTTP roles, multi-language and wire-name isolation, shipped-name preservation, unknown units/targets, unsupported profiles, and name collisions/synthesized targets. Routing cases use natural prompts; capability cases explicitly invoke the skill to isolate behavior from routing. Deterministic graders require skill loading and a successful naming-reference read, check keyed naming outcomes, and reject network/edit/validation tool calls. They do not claim that planned names have been verified by a real emitter.

From `.github/skills`, using the repository's Vally 0.14 installation:

```powershell
vally lint azure-typespec-author --strict
vally lint -e azure-typespec-author\evals\eval.yaml --strict
vally eval -e azure-typespec-author\evals\eval.yaml --workers 1 --output jsonl --output-dir ..\..\artifacts\naming-evals
```

The eval explicitly mounts the target and the competitors needed for boundary tests; do not add `--skill-dir` pointing at all repository skills. Use `--tag case=smoke` for the member-naming smoke case. Run serially if the local Copilot executor reports `Cannot set session filesystem provider while sessions are active`. The shared skill-eval pipeline discovers this suite; the existing live benchmarks remain separate.
