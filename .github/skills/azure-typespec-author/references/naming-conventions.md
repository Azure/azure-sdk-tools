# SDK Naming Conventions

Apply SDK naming guidance while authoring, not only after generation or a linter failure. This dispatcher selects language/service-specific references; it is not a universal naming policy.

## Select

Use the user's confirmed targets, `tspconfig.yaml` emitter/options configuration, and SDK project configuration to identify SDK languages and ARM versus data-plane. Normalize `C#`, `.NET`, and `csharp` to `csharp`. An OpenAPI-only configuration does not establish an SDK target. Ask before language-specific edits when the target or service type is unknown.

An unknown SDK target is missing intake information, not an unsupported profile. Ask which languages are targeted; do not silently label it "not covered" and omit naming guidance.

| Language        | Service type     | Profile                                                                            |
| --------------- | ---------------- | ---------------------------------------------------------------------------------- |
| `csharp`        | ARM / management | [C# member naming](csharp-naming.md) and [C# model naming](csharp-model-naming.md) |
| `csharp`        | Data-plane       | Not covered by this initial profile; do not apply ARM naming rules                 |
| Other languages | Any              | Not yet covered; retain applicable shared API guidance, without borrowing C# rules |

For multiple targets, load only matching profiles and record coverage per target. Missing coverage is not approval of a name. If the requested naming change needs an unsupported profile, obtain authoritative language guidance or ask for clarification; do not invent rules.

## Plan

1. Inventory new/modified names in the requested scope, including affected references and existing `@clientName`/`@@clientName` customizations. Distinguish service-owned declarations, imported common types, and emitter-synthesized SDK artifacts.
2. Record: **TypeSpec target | current SDK name | proposed SDK name | language scope | rule/exception | compatibility evidence**. Names refer to the effective SDK surface, not just TypeSpec spelling.
3. Confirm semantics before choosing a name: HTTP role, property type, duration units, resource inheritance, and domain context. Ask about unknown units or ambiguous names rather than guessing.
4. Check the released SDK API or other supplied release evidence before renaming an existing member. A preview API version does not prove an SDK name is unshipped. Preserve shipped names unless an intentional breaking change is explicitly authorized; restore an unintentionally changed shipped name instead of inventing a third name. If release evidence is unavailable, flag that decision as blocked rather than assuming safety.
5. Do not rename unrelated existing API, redefine common ARM types to satisfy a naming preference, or silently resolve a name collision. Record exceptions and unresolved decisions in the plan.

## Apply

- For an SDK-only rename of a TypeSpec-defined symbol, use a language-scoped [TCGC `clientName` decorator](https://azure.github.io/typespec-azure/docs/libraries/typespec-client-generator-core/reference/decorators/#clientname). An omitted scope affects all languages.
- Reuse the project's client customization file, typically `client.tsp`, and confirm that the SDK generation entrypoint loads it. Follow its imports and namespace resolution; avoid circular imports or duplicate/conflicting decorators.
- For example, with `using Azure.ClientGenerator.Core` and the target in scope: `@@clientName(WidgetProperties.enabled, "IsEnabled", "csharp");`.
- Keep TypeSpec/wire names, serialized enum values, routes, and other language customizations unchanged for SDK-only naming. Do not use `@encodedName` or rename a source property to fix a C# spelling.
- Do not hand-edit generated C#. For synthesized artifacts without a targetable TypeSpec declaration, report the limitation and the required emitter/customization follow-up; do not fabricate a decorator target.

## Validate

Compare edits with every naming decision and exception. Check decorator targets, language scopes, customization loading, name collisions, unchanged wire names, and unaffected SDK languages. Use generated API output and the released baseline when available; distinguish a planned name from a verified generated name.

Run normal TypeSpec validation/compilation and installed naming linters. [Azure/typespec-azure#4442](https://github.com/Azure/typespec-azure/issues/4442) tracks the .NET management linter work; it does not establish which rules are installed in a project. Lint success does not replace contextual naming or SDK compatibility checks.

## Extend

Add a referenced profile for each new language/service pair, with authoritative sources, exceptions, and scope/compatibility evals. Add its row above; keep language rules out of the shared workflow.
