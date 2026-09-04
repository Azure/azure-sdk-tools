# Downstream SDK Breaking Cases

Classify only from normalized base/current TCGC facts and changed customization decorators. Compare public, language-neutral SDK behavior; do not classify the REST wire contract here.

| Case                                | Approve when existing SDK use can break                                                                                                                  |
| ----------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Client ownership or method location | A method moves between clients/operation groups, or its owning public client identity changes.                                                           |
| Method parameters                   | Ordered parameters change name, type, position, optionality, or `onClient`; a required parameter is added; or an existing parameter is removed.          |
| Method response                     | The public response type changes, appears, or disappears in a way that changes generated signatures or runtime result handling.                          |
| Method kind                         | `basic`, `paging`, `lro`, or `lropaging` changes and alters the generated invocation/result pattern.                                                     |
| Access and reachability             | A public client, method, model, property, or enum becomes internal/unreachable, or usage metadata removes it from required public input/output surfaces. |
| Model property                      | Public property name, serialized SDK-facing identity, type, or optionality changes incompatibly.                                                         |
| Enum                                | Enum/type name changes, a value is removed/renamed, or extensibility changes so previously accepted values no longer work.                               |
| Paging                              | Item/next-link/page-size/continuation segments, reinjected parameters, or next-link verb change generated iteration behavior.                            |
| LRO                                 | Polling/final-state/result/envelope/step metadata changes generated poller behavior or returned results.                                                 |
| Customization                       | Primary client naming, flattening, or location decorators change public symbols, parameter shape, or ownership.                                          |

Reject when the fact is purely additive and preserves existing signatures and runtime behavior, affects only internal/unreachable types, or lacks a source-linked causal change. Do not infer a break from YAML order, aliases, duplicate namespace views, artifact paths, or raw serialization differences.

Reject a property-removal or response-type candidate when the change only replaces an implicit response shape with an explicit response model: the same response/header member is still exposed with the same SDK type and optionality, and the operation still returns it. Moving the member into the explicit response model is not an SDK break. Approve the candidate if the member is actually removed or its public SDK contract changes.

For a newly added required model property, use the model direction. Approve it when the model is request/input or both input and output, because existing callers must provide the new member. Reject it when the model is response/output-only; receiving an additional required response member is additive for existing callers.

Use cross-language definition IDs as primary identity. A guarded composite identity is acceptable only when the deterministic input lacks one. Compare method parameters at the SDK method layer; do not conflate them with nested HTTP parameters or `bodyParam`.

Containment and metadata changes matter when they alter a public shape: arrays, tuples, dictionaries, nullable/union variants, discriminators, inheritance, paging, and LRO step unions must be interpreted through their normalized TCGC fields.

Required reference case: if `ScenarioRuns.cancel` or `ScenarioConfigurations.execute` changes from `basic` with no response type to `lro` returning `ScenarioRun` with Location polling metadata, approve the downstream candidate even when method parameters and REST compatibility are unchanged. The generated call/result pattern changed.
