# Optional Client Configuration

Run this step after the TypeSpec source plan and before applying changes. It is
separate from the source plan because `client.tsp` affects generated clients,
not the REST contract.

## Decide

Skip this step without prompting when the request cannot affect the generated
client surface, such as documentation-only edits. For SDK-visible source
changes, client configuration analysis is recommended and included by default.
If the request already explicitly asks for naming or client configuration,
continue without another decision. Otherwise, use `ask_user` to offer:

- **Keep client configuration analysis (Recommended)**
- **Opt out**

Explain:

> Client configuration analysis is included by default for SDK-visible changes.
> Would you like to keep it, or opt out for this authoring request? If you opt
> out, you can run the analysis later when entering the Azure SDK release
> workflow using AzSDKToolsAgent: <https://aka.ms/azsdk/agent>.

Do not invoke `azsdk-common-typespec-naming` before the user decides. When the
user opts out, do not create or edit `client.tsp`.

Keep the source and client configuration plans visibly separate so the user can
approve the source changes while declining the client configuration portion.

## Build the Client Configuration Plan

1. Identify confirmed SDK languages, ARM/data-plane context, changed
   declarations, and release compatibility evidence.
2. Read the existing `client.tsp` and generation entrypoint **without editing**
   them. Record existing `@clientName`/`@@clientName` overrides, whether the
   customization file is loaded, and whether proposed targets resolve.
3. Invoke `azsdk-common-typespec-naming` in guidance mode. It selects only
   applicable profiles; currently C# ARM is covered.
4. Produce a distinct client configuration plan containing targets, current and
   proposed SDK names, language scopes, compatibility evidence, exceptions,
   uncovered targets, and blockers. Preserve shipped SDK names, wire contracts,
   and other-language overrides.

Naming-only requests route directly to `azsdk-common-typespec-naming`; they do
not need the full authoring workflow.

## Apply

After authorization, invoke `azsdk-common-typespec-naming` in apply mode. It
delegates SDK-only overrides to `azsdk_customized_code_update` with
`editScope: SpecInputs`. The customization tool is the sole writer for
`client.tsp`; do not duplicate its mutation with file-edit or shell tools.
