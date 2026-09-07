# Recent SDK AI Bot PR Deployment Impact

Audit window: `2026-08-07` through `2026-09-07`, using first-parent merges to
`origin/main` that touched `tools/sdk-ai-bots`.

| PR | Date | Change | Deployment impact and disposition |
|---|---|---|---|
| [#16887](https://github.com/Azure/azure-sdk-tools/pull/16887) | 2026-09-03 | Knowledge sync uses Azure credentials for OpenAI | High. The sync task now runs inside `AzureCLI@2` with WIF, credential chains prefer WIF, and the deployment principal receives OpenAI and data-plane roles. |
| [#16498](https://github.com/Azure/azure-sdk-tools/pull/16498) | 2026-09-03 | Add AzSDK Tools Agent tenant | High. Prod now monitors the added channel, source-controlled prod routing maps it to `azsdk_tools_agent_qa_bot`, and hosted images expose the required pipeline-detail MCP tools. |
| [#16885](https://github.com/Azure/azure-sdk-tools/pull/16885) | 2026-09-02 | Add JavaScript release-tool docs | Data only. Covered by the scheduled knowledge sync and its now-blocking test suite. |
| [#16843](https://github.com/Azure/azure-sdk-tools/pull/16843) | 2026-09-01 | Remove legacy JS release tools | Data only. Knowledge configuration points at the replacement external source; no Azure resource change. |
| [#16849](https://github.com/Azure/azure-sdk-tools/pull/16849) | 2026-08-31 | Enable wiki search in retrieval API | Runtime/data flow. Existing shared Search schema supports wiki fields; wiki settings and indexer name are now seeded explicitly. |
| [#16842](https://github.com/Azure/azure-sdk-tools/pull/16842) | 2026-08-31 | Recover corrupted agent tool history | Runtime only. Existing agent-server image and Cosmos conversation containers carry the change. |
| [#16777](https://github.com/Azure/azure-sdk-tools/pull/16777) | 2026-08-28 | Add chatbot evolution agent | High. Provisioning now owns `qa-records`; prod selects dev as its candidate environment; full-stack prod deploys and authorizes the hosted identity; the feedback job restores candidate knowledge before analysis and after mutation, then validates prod. |
| [#16718](https://github.com/Azure/azure-sdk-tools/pull/16718) | 2026-08-27 | Prevent DNS-rebinding SSRF | Runtime only. Included in agent images; no new resource or configuration dependency. |
| [#16836](https://github.com/Azure/azure-sdk-tools/pull/16836) | 2026-08-27 | Recover malformed Foundry SSE | Runtime only. Included in agent-server and hosted-agent images. |
| [#16829](https://github.com/Azure/azure-sdk-tools/pull/16829) | 2026-08-26 | Fix wiki async dependency | Dependency only. Covered by wiki CI and the scheduled build environment. |
| [#16463](https://github.com/Azure/azure-sdk-tools/pull/16463) | 2026-08-26 | Add generated wiki retrieval | High. Provisioning already creates the wiki container and Search resources; deployment now seeds builder settings, aligns the repair script, and starts the wiki indexer immediately after generation. |
| [#16758](https://github.com/Azure/azure-sdk-tools/pull/16758) | 2026-08-18 | Function `js-yaml` update | Build/runtime dependency. Function images now use `npm ci`, and function CI executes its source test before build. |
| [#16742](https://github.com/Azure/azure-sdk-tools/pull/16742) | 2026-08-17 | Knowledge-sync `brace-expansion` update | Build dependency. Deterministic `npm ci`; failures in the 44-test suite now fail CI. |
| [#16682](https://github.com/Azure/azure-sdk-tools/pull/16682) | 2026-08-17 | Knowledge-sync `js-yaml` update | Build/data parser dependency. Covered by deterministic install and blocking tests. |
| [#16695](https://github.com/Azure/azure-sdk-tools/pull/16695) | 2026-08-17 | Function `nanoid` update | Transitive build dependency. Covered by deterministic image install and function test/build. |
| [#16714](https://github.com/Azure/azure-sdk-tools/pull/16714) | 2026-08-12 | Include failed-test artifact paths | Hosted-agent tool contract. Images now pin released `azsdk` 0.6.43 and allow both failed-test detail tools. |
| [#16702](https://github.com/Azure/azure-sdk-tools/pull/16702) | 2026-08-11 | Update Teams AI message labels | Runtime only. Covered by frontend tests/build; no infrastructure or configuration change. |
| [#16696](https://github.com/Azure/azure-sdk-tools/pull/16696) | 2026-08-10 | Add MCP documentation source | Data only. Covered by knowledge sync and Search indexing. |
| [#16694](https://github.com/Azure/azure-sdk-tools/pull/16694) | 2026-08-10 | Frontend deployment/version update | Build flow. The consolidated deployment ignores legacy mutable Docker tags and now uses `npm ci` before immutable ACR builds. |
| [#16657](https://github.com/Azure/azure-sdk-tools/pull/16657) | 2026-08-10 | Add Alloy docs/sample processing | Data and tenant capability. The sync package carries the processor and source configuration; no new Azure resource is required. |

## Resulting data flows

1. Knowledge sync authenticates with the selected environment's WIF service
   connection, writes the `knowledge` container, then starts
   `AI_SEARCH_INDEXER`.
2. Wiki build reads that corpus, writes the `wiki` container, then starts
   `AI_SEARCH_WIKI_INDEXER`.
3. The feedback job restores candidate dev knowledge, runs production
   evolution analysis against isolated dev clients, restores dev again, and
   only then validates closed issues against production.
4. Teams channel subscriptions in the suite exactly match each environment's
   uploaded `channel.yaml`; every route tenant must exist in `tenant.yaml`.

## Operational follow-up

- Provision each environment once to create `qa-records`, seed the new App
  Configuration keys, and apply deployment-principal RBAC.
- Register and authorize the knowledge-sync, wiki-build, hosted-agent, and
  feedback-job YAML definitions. Grant the feedback pipeline's build identity
  permission to queue the knowledge-sync definition.
- Preview remains intentionally blocked until its subscription and backend
  application placeholders are replaced.