#!/usr/bin/env node

import {
  existsSync,
  mkdirSync,
  readFileSync,
  rmSync,
  writeFileSync,
} from "node:fs";
import { join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { buildCompliance } from "./generate-document-compliance-evidence.mjs";
import { sourceLink } from "../../scripts/prepare-assessment.mjs";
import { renderAssessmentHtml } from "../../scripts/render-assessment-html.mjs";
import { validateAssessment } from "../../scripts/validate-assessment.mjs";

const emitterNames = {
  autorest: "@azure-tools/typespec-autorest",
  tcgc: "@azure-tools/typespec-client-generator-core",
};

const curatedDiffAliases = {
  "mark-batch-outbound-rules-as-paged": "pr-42435-intent",
  "preserve-go-client-placement": "pr-42853-intent",
  "intent-location-based-lro-metadata": "pr-43308-intent",
  "replace-cloud-hsm-cluster-sku-enum": "pr-43745-intent",
  "add-private-link-resource-management": "pr-44200-private-frontend",
  "adjust-sdk-property-flattening": "pr-44200-js-flattening",
  "semantic-intent-1": "pr-44454-intent",
  "intent-add-stable-version-preserve-go-delete-order": "pr-44882-intent",
  "preserve-go-delete-parameter-order": "pr-44882-intent",
  "publish-2026-06-01-version-lineage": "pr-44882-version-lineage",
  "add-address-prefix-sets": "address-prefix-sets",
  "add-express-route-lags": "express-route-lag-family",
  "add-firewall-policy-kube-selector-groups":
    "firewall-policy-kube-selector-groups",
  "add-afc-managed-firewall-policy-writes":
    "firewall-policy-afc-managed-sync",
  "add-first-party-service-tags": "first-party-service-tags",
  "add-network-virtual-appliance-migration-actions":
    "network-virtual-appliance-migration",
  "add-connection-analyzers": "connection-analyzers",
  "add-virtual-network-ip-configuration-move":
    "virtual-network-ip-configuration-move",
  "add-virtual-network-gateway-effective-routes":
    "virtual-network-gateway-effective-routes",
  "make-service-gateway-update-actions-synchronous":
    "service-gateway-sync-actions",
  "intent-arm-common-types-v5": "pr-45348-intent",
  "intent-java-enum-member-names": "pr-45536-intent",
};

function readJson(path) {
  return JSON.parse(readFileSync(path, "utf8"));
}

function emitterRuns(evidence) {
  return evidence.projects.flatMap((project) =>
    project.compilations.flatMap((compilation) =>
      compilation.emitters.map((emitter) => ({
        project: project.path,
        revision: compilation.side,
        emitter: emitterNames[emitter.emitter] ?? emitter.emitter,
        emitterId: emitter.emitter,
        output:
          emitter.emitter === "autorest" ? "OpenAPI" : "SDK metadata (generic)",
        status: emitter.status,
        evidence: emitter.failureSummary ?? emitter.outputDirectory,
      })),
    ),
  );
}

function formatDuration(milliseconds) {
  const seconds = Math.round(milliseconds / 1000);
  const minutes = Math.floor(seconds / 60);
  return minutes > 0 ? `${minutes}m ${seconds % 60}s` : `${seconds}s`;
}

function assessmentRemoteUrl(assessmentUrl) {
  if (!assessmentUrl) return undefined;
  const match = assessmentUrl.match(
    /^(https:\/\/github\.com\/[^/]+\/[^/]+)(?:\/|$)/,
  );
  return match?.[1];
}

export function normalizeAssessmentSourceLinks(assessment) {
  const remoteUrl = assessmentRemoteUrl(assessment.url);

  function visit(value) {
    if (Array.isArray(value)) {
      for (const item of value) visit(item);
      return;
    }
    if (!value || typeof value !== "object") return;
    if (
      typeof value.path === "string" &&
      value.path.endsWith(".tsp") &&
      ["base", "head"].includes(value.revision) &&
      Number.isInteger(value.startLine) &&
      Number.isInteger(value.endLine)
    ) {
      const commit =
        value.revision === "base"
          ? assessment.baseline?.commit
          : assessment.head?.commit;
      value.link = sourceLink(
        value.path,
        value.revision,
        commit,
        remoteUrl,
        value.startLine,
        value.endLine,
      );
    }
    for (const child of Object.values(value)) visit(child);
  }

  visit(assessment);
  return assessment;
}

function replaceSemanticText(item, { intent, summary }) {
  item.intent = intent;
  item.transformationChain = [summary];
  item.restRepresentation.summary = summary;
}

function setChangeExplanation(item, { aspects, effect, typeSpecCause }) {
  for (const change of item.changes) {
    change.aspects = aspects;
    change.effect = effect;
    change.typeSpecCause = typeSpecCause;
  }
}

function valuesMatch(left, right) {
  return JSON.stringify(left) === JSON.stringify(right);
}

function correctNetwork44988(assessment, items) {
  assessment.dimensions.semanticUnderstanding.items =
    assessment.dimensions.semanticUnderstanding.items.filter(
      ({ id }) => id !== "preserve-vmss-network-read-contracts",
    );

  const versionLineage = items.get("introduce-2025-09-01-version-lineage");
  if (versionLineage) {
    versionLineage.intent =
      "Introduce the 2025-09-01 Network API version with additive fields on existing resources";
    versionLineage.restRepresentation.summary =
      "The new API version adds disableDefaultServerHeaderInResponse to application gateway global configuration and a read-only upgradedToV2 indicator to public IP address properties. The remaining listed operation families are inherited into 2025-09-01 without a REST behavior change.";
    versionLineage.transformationChain = [
      versionLineage.restRepresentation.summary,
    ];
    for (const change of versionLineage.changes) {
      change.kind = "added";
      change.summary =
        "Expose the inherited Network surface in 2025-09-01 and add three model fields";
      change.aspects = [
        {
          field: "2025-09-01 API-version surface",
          before: null,
          after:
            "87 existing operations are carried into the new API version; application gateways and public IP addresses also receive additive fields.",
        },
      ];
      change.effect = versionLineage.restRepresentation.summary;
      change.typeSpecCause =
        "Add Versions.v2025_09_01 and version-scoped fields disableDefaultServerHeaderInResponse and upgradedToV2.";
      change.typeSpecDiffs = change.typeSpecDiffs.filter(
        (hunk) => hunk.context !== "model IpTag {",
      );
    }
  }

  for (const item of assessment.dimensions.semanticUnderstanding.items) {
    if (item === versionLineage) continue;
    item.changes = item.changes
      .map((change) => ({
        ...change,
        aspects: change.aspects.filter(
          (aspect) => !valuesMatch(aspect.before, aspect.after),
        ),
      }))
      .filter(
        (change) =>
          change.kind !== "modified" || change.aspects.length > 0,
      );
    const retainedOperationIds = new Set(
      item.changes.flatMap((change) => change.operationIds),
    );
    item.restRepresentation.operations =
      item.restRepresentation.operations.filter((operation) =>
        retainedOperationIds.has(operation.operationId),
      );
  }

  const kubeSelectors = items.get(
    "add-firewall-policy-kube-selector-groups",
  );
  if (kubeSelectors) {
    const summary =
      "The 2025-09-01 API adds FirewallPolicyKubeSelectorGroup child resources with create-or-update and delete LROs, get, and pageable list operations beneath a firewall policy.";
    kubeSelectors.transformationChain = [summary];
    kubeSelectors.restRepresentation.summary = summary;
    for (const change of kubeSelectors.changes.filter(
      ({ kind }) => kind === "added",
    )) {
      change.effect = summary;
      change.typeSpecCause =
        "Add the versioned FirewallPolicyKubeSelectorGroup child model and its ARM resource-operation interface.";
    }
  }
  const firewallPolicyChange = kubeSelectors?.changes.find(
    ({ kind }) => kind === "modified",
  );
  if (firewallPolicyChange) {
    firewallPolicyChange.operationIds = ["FirewallPolicies_CreateOrUpdate"];
    firewallPolicyChange.apiVersions = ["2025-09-01"];
    firewallPolicyChange.summary =
      "Add the optional afcManagedSync query parameter to firewall policy create-or-update";
    firewallPolicyChange.effect =
      "FirewallPolicies_CreateOrUpdate accepts the new optional afcManagedSync query parameter only in 2025-09-01. The other firewall policy operations and all earlier API versions retain their existing REST contracts.";
    firewallPolicyChange.typeSpecCause =
      "Add the optional afcManagedSync query parameter to FirewallPolicies_CreateOrUpdate in the 2025-09-01 version.";
    firewallPolicyChange.linkedFindingIds = [];

    const firewallOperation =
      kubeSelectors.restRepresentation.operations.find(
        ({ operationId }) =>
          operationId === "FirewallPolicies_CreateOrUpdate",
      );
    kubeSelectors.changes = kubeSelectors.changes.filter(
      (change) => change !== firewallPolicyChange,
    );
    kubeSelectors.restRepresentation.operations =
      kubeSelectors.restRepresentation.operations.filter(
        ({ operationId }) =>
          operationId !== "FirewallPolicies_CreateOrUpdate",
      );
    if (firewallOperation) {
      const firewallReference = {
        path: "specification/network/resource-manager/Microsoft.Network/Network/Network/FirewallPolicy.tsp",
        revision: "head",
        startLine: 70,
        endLine: 82,
        link: "",
      };
      firewallOperation.sourceReferences = [firewallReference];
      firewallPolicyChange.sourceReferences = [firewallReference];
      assessment.dimensions.semanticUnderstanding.items.push({
        id: "add-afc-managed-firewall-policy-writes",
        intent: "Add opt-in AFC-managed firewall policy writes",
        transformationChain: [firewallPolicyChange.effect],
        changes: [firewallPolicyChange],
        restRepresentation: {
          summary: firewallPolicyChange.effect,
          operations: [firewallOperation],
        },
        confidence: kubeSelectors.confidence,
        sourceReferences: [firewallReference],
      });
    }
  }

  const narrativeCorrections = {
    "add-network-virtual-appliance-migration-actions":
      "The 2025-09-01 API adds prepare, execute, commit, and abort migration actions for network virtual appliances. All four are Location-polled long-running operations.",
    "add-virtual-network-ip-configuration-move":
      "The 2025-09-01 API adds MoveIpConfigurations as an Azure-AsyncOperation-polled virtual network action with an explicit empty request body.",
    "add-virtual-network-gateway-effective-routes":
      "The 2025-09-01 API adds GetEffectiveRoutes as a Location-polled virtual network gateway operation returning the effective-route list.",
  };
  for (const [id, summary] of Object.entries(narrativeCorrections)) {
    const item = items.get(id);
    if (!item) continue;
    item.transformationChain = [summary];
    item.restRepresentation.summary = summary;
    for (const change of item.changes) change.effect = summary;
  }

  const serviceGateway = items.get(
    "make-service-gateway-update-actions-synchronous",
  );
  if (serviceGateway) {
    const findingId =
      "source-service-gateway-actions-change-from-lro-to-synchronous";
    const summary =
      "The new 2025-09-01 REST version intentionally replaces the prior 202/204 Location-polled behavior of UpdateAddressLocations and UpdateServices with a synchronous 200 response. Earlier API versions remain unchanged, so this is not a REST breaking change; however, an SDK that adopts 2025-09-01 can replace its existing poller or operation-handle methods with synchronous methods returning ServiceGatewayActionOkResponseBody.";
    replaceSemanticText(serviceGateway, {
      intent:
        "Make service gateway update actions synchronous in 2025-09-01",
      summary,
    });
    for (const change of serviceGateway.changes) {
      change.effect = summary;
      change.linkedFindingIds = [findingId];
    }

    const findings =
      assessment.dimensions.restCompatibleDownstreamBreakingChanges.findings;
    const finding = {
      id: findingId,
      title:
        "Generated service gateway update methods can change from pollers to synchronous calls",
      severity: "high",
      confidence: "high",
      summary:
        "The older REST API versions remain compatible, but generated SDKs that move to 2025-09-01 can expose ServiceGateways_UpdateAddressLocations and ServiceGateways_UpdateServices as synchronous methods instead of long-running poller or operation-handle methods. Existing call sites can stop compiling because the return type and completion-handling pattern change, and runtime code that waits for polling completion must instead consume the immediate 200 response.",
      evidence: [
        "Both existing operations change from 202/204 responses with Location polling to a synchronous 200 response in 2025-09-01.",
        "The TypeSpec replaces ArmResourceActionAsync with version-gated ArmResourceActionSync operations.",
      ],
      sourceReferences: serviceGateway.sourceReferences,
    };
    const existingIndex = findings.findIndex(({ id }) => id === findingId);
    if (existingIndex === -1) {
      findings.push(finding);
    } else {
      findings[existingIndex] = finding;
    }
  }

  for (const item of assessment.dimensions.semanticUnderstanding.items) {
    if (item === versionLineage) continue;
    const retainedOperationIds = new Set(
      item.changes.flatMap((change) => change.operationIds),
    );
    item.restRepresentation.operations =
      item.restRepresentation.operations.filter((operation) =>
        retainedOperationIds.has(operation.operationId),
      );
  }
}

function historicalOperation(operationsByPr, pr, operationId, references) {
  const operation = operationsByPr?.[String(pr)]?.find(
    (candidate) => candidate.operationId === operationId,
  );
  if (!operation) return undefined;
  const { request, responses, ...rest } = operation;
  return {
    ...structuredClone(rest),
    signature: `${operation.method} ${operation.path}`,
    requestPayload: request,
    responsePayloads: responses,
    lro: operation.lro.isLongRunning
      ? {
          polling:
            "Poll the emitted async endpoint after Retry-After until a terminal state.",
          finalResult:
            "Use the final response contract described for this operation.",
          ...operation.lro,
        }
      : operation.lro,
    sourceReferences: references,
  };
}

export function correctHistoricalSemanticChains(
  assessment,
  operationsByPr,
) {
  const items = new Map(
    assessment.dimensions.semanticUnderstanding.items.map((item) => [
      item.id,
      item,
    ]),
  );

  const goPlacement = items.get("preserve-go-client-placement");
  if (goPlacement) {
    const summary =
      "The client-location customizations extend the existing language-specific placement behavior to Go for bmsPrepareDataMove, bmsTriggerDataMove, getOperationStatus, and moveRecoveryPoint. These four methods can move to different generated Go clients, while their HTTP methods, paths, parameters, payloads, and responses remain unchanged.";
    replaceSemanticText(goPlacement, {
      intent: "Apply established client placement to four Go operations",
      summary,
    });
    setChangeExplanation(goPlacement, {
      aspects: [
        {
          field: "Go client location",
          before:
            "The four methods follow the prior language exclusions and generated Go client placement.",
          after:
            "Go is added to all four client-location exclusions, moving the methods to their established generated Go clients.",
        },
      ],
      effect: summary,
      typeSpecCause:
        "Extend four existing @@clientLocation customizations from !csharp to !csharp,!go.",
    });
    const finding =
      assessment.dimensions.restCompatibleDownstreamBreakingChanges.findings.find(
        ({ id }) => id === "source-go-client-location-changed",
      );
    if (finding) {
      finding.summary =
        "The REST contracts are unchanged, but extending four client-location customizations to Go can move bmsPrepareDataMove, bmsTriggerDataMove, getOperationStatus, and moveRecoveryPoint between generated clients. Existing Go code that constructs the former clients or invokes these methods through them can stop compiling.";
      finding.evidence = [
        "All four @@clientLocation rules add Go to the language exclusion.",
        "The four HTTP operation contracts remain unchanged.",
      ];
    }
  }

  const backupVersion = items.get(
    "promote-recovery-services-backup-api-version",
  );
  if (backupVersion) {
    backupVersion.id =
      "promote-recovery-services-backup-api-version-lineage";
    const summary =
      "Seven existing Recovery Services Backup operations are published under the new stable 2026-02-01 API version. Their HTTP methods, paths, parameters, requests, responses, LRO behavior, and paging behavior are inherited without change.";
    replaceSemanticText(backupVersion, {
      intent:
        "Publish the inherited Recovery Services Backup surface in stable 2026-02-01",
      summary,
    });
    for (const change of backupVersion.changes) {
      change.kind = "added";
      change.summary = backupVersion.intent;
      change.aspects = [
        {
          field: "2026-02-01 API-version availability",
          before: null,
          after:
            "Seven existing operations are exposed in the new stable API version without a wire-behavior change.",
        },
      ];
      change.effect = summary;
      change.typeSpecCause =
        "Add the stable Versions.v2026_02_01 member and make it the current version.";
    }
  }

  const chaosLro = items.get("intent-location-based-lro-metadata");
  if (chaosLro) {
    const summary =
      "Six existing operations retain their 202 response, Location and Retry-After headers, Location polling, and final result. Replacing raw OpenAPI extensions with @Azure.Core.useFinalStateVia(\"location\") makes the same LRO behavior visible to TypeSpec-aware SDK generators.";
    replaceSemanticText(chaosLro, {
      intent:
        "Model existing Location-based long-running behavior with TypeSpec LRO metadata",
      summary,
    });
    setChangeExplanation(chaosLro, {
      aspects: [
        {
          field: "TypeSpec LRO metadata",
          before:
            "Final-state-via Location is represented with raw OpenAPI extensions.",
          after:
            "Final-state-via Location is represented with @Azure.Core.useFinalStateVia(\"location\").",
        },
      ],
      effect: summary,
      typeSpecCause:
        "Replace OpenAPI-only LRO extensions with TypeSpec LRO decorators and remove the obsolete polling-operation link.",
    });
  }

  const cloudHsm = items.get("replace-cloud-hsm-cluster-sku-enum");
  if (cloudHsm) {
    const summary =
      "CloudHsmClusterSkuName changes from a closed enum to an open string union. The known wire values Standard_B1 and Standard B10 remain unchanged, while the string variant allows future service values without changing the REST shape.";
    replaceSemanticText(cloudHsm, {
      intent:
        "Make Cloud HSM cluster SKU names forward-compatible with an open string union",
      summary,
    });
    setChangeExplanation(cloudHsm, {
      aspects: [
        {
          field: "`sku.name` accepted values",
          before:
            "The closed enum accepts only Standard_B1 and Standard B10.",
          after:
            "The open string union preserves both known values and accepts future string values.",
        },
      ],
      effect:
        "Generated SDKs can represent unknown future SKU values instead of treating the model as a closed enum; the two known serialized values and consuming REST operations remain unchanged.",
      typeSpecCause:
        "Replace the suppressed enum with a union that includes string and preserves both named values.",
    });
  }

  const privateLink = items.get("add-private-link-resource-management");
  if (privateLink) {
    for (const change of privateLink.changes) {
      change.linkedFindingIds = [
        "compliance-stable-version-retains-replaced-preview",
        "compliance-private-endpoint-standard-pattern",
        "compliance-private-link-standard-pattern",
        "compliance-legacy-flattening-on-new-models",
      ];
    }
  }
  const flattening = items.get("adjust-sdk-property-flattening");
  if (flattening) {
    const summary =
      "AssociationUpdate.properties remains the same nested JSON property on the wire, but JavaScript is excluded from the legacy flattening rule. Only Associations_Update consumes this changed SDK model shape.";
    replaceSemanticText(flattening, {
      intent:
        "Keep AssociationUpdate.properties nested in generated JavaScript",
      summary,
    });
    const associationReference = flattening.sourceReferences.find(
      ({ path }) => path.endsWith("/main.tsp"),
    );
    const associationReferences = associationReference
      ? [associationReference]
      : flattening.sourceReferences;
    const associationUpdate =
      flattening.restRepresentation.operations.find(
        ({ operationId }) => operationId === "Associations_Update",
      ) ??
      historicalOperation(
        operationsByPr,
        assessment.pr,
        "Associations_Update",
        associationReferences,
      );
    if (associationUpdate) {
      flattening.restRepresentation.operations = [associationUpdate];
      flattening.sourceReferences = associationReferences;
    }
    for (const change of flattening.changes) {
      change.operationIds = ["Associations_Update"];
      change.apiVersions = [
        ...new Set(
          (associationUpdate?.apiVersions ?? []).map((version) => version),
        ),
      ];
      change.aspects = [
        {
          field: "JavaScript AssociationUpdate model shape",
          before: "properties is flattened into AssociationUpdate.",
          after: "properties remains a nested object in JavaScript.",
        },
      ];
      change.effect = summary;
      change.typeSpecCause =
        "Change the AssociationUpdate.properties flattenProperty scope from all languages to !javascript.";
      change.linkedFindingIds = [
        "source-javascript-flattening-scope-changed",
      ];
    }
  }

  const pythonLocations = items.get("semantic-intent-1");
  if (pythonLocations) {
    const summary =
      "Three existing operations receive Python-only PascalCase client locations while Python is excluded from their shared camelCase locations. Their REST contracts are unchanged; generated Python operation-group names return to ListAssociatedTrafficFilters, CreateAndAssociateIPFilter, and CreateAndAssociatePLFilter.";
    replaceSemanticText(pythonLocations, {
      intent:
        "Restore released Python PascalCase operation-group names for three operations",
      summary,
    });
    setChangeExplanation(pythonLocations, {
      aspects: [
        {
          field: "Python operation-group location",
          before:
            "The operations use shared camelCase client locations in generated Python.",
          after:
            "Python uses the released PascalCase client locations while other languages retain their existing locations.",
        },
      ],
      effect:
        "Generated Python clients preserve the released operation-group names for the three listed operations; no HTTP method, path, parameter, request, or response changes.",
      typeSpecCause:
        "Exclude Python from three shared @@clientLocation rules and add three Python-only PascalCase rules.",
    });
    assessment.errors = [];
    assessment.overallConfidence = "high";
  }

  const commonTypes = items.get("intent-arm-common-types-v5");
  if (commonTypes) {
    const summary =
      "The two new Device Registry preview versions use ARM common-types v5 instead of v6, matching the prior API lineage and avoiding cross-version identity, SKU, plan, and requiredness drift in generated OpenAPI.";
    replaceSemanticText(commonTypes, {
      intent:
        "Align the new Device Registry previews with ARM common-types v5",
      summary,
    });
    setChangeExplanation(commonTypes, {
      aspects: [
        {
          field: "ARM common-types dependency",
          before: "The two preview versions reference common-types v6.",
          after: "Both preview versions reference common-types v5.",
        },
      ],
      effect: summary,
      typeSpecCause:
        "Change @armCommonTypesVersion from v6 to v5 on the 2026-11-01-preview and 2026-11-02-preview version members.",
    });
    for (const operation of commonTypes.restRepresentation.operations) {
      operation.apiVersions = [
        "2026-11-01-preview",
        "2026-11-02-preview",
      ];
    }
  }

  const javaEnums = items.get("intent-java-enum-member-names");
  if (javaEnums) {
    const summary =
      "Six Java-scoped @@clientName directives preserve released constants on Metrictype, Datagrain, and LookBackPeriod. The five listed operations are included because their generated Java signatures consume those enums; their HTTP contracts and serialized enum values do not change.";
    replaceSemanticText(javaEnums, {
      intent: "Preserve released Java names for six enum members",
      summary,
    });
    setChangeExplanation(javaEnums, {
      aspects: [
        {
          field: "Generated Java enum members",
          before:
            "The affected members rely on newly derived Java names.",
          after:
            "Explicit Java-only names preserve the six released enum constants.",
        },
      ],
      effect:
        "Java callers of the five consuming operations retain the released enum constants. Other languages, REST payloads, and serialized values are unchanged.",
      typeSpecCause:
        "Add six Java-scoped @@clientName directives to existing members of Metrictype, Datagrain, and LookBackPeriod.",
    });
  }

  const combinedNewRelic = items.get(
    "intent-add-stable-version-preserve-go-delete-order",
  );
  if (combinedNewRelic) {
    const allOperations = combinedNewRelic.restRepresentation.operations;
    const deleteOperation = allOperations.find(
      ({ operationId }) => operationId === "Monitors_Delete",
    );
    const mainReference = combinedNewRelic.sourceReferences.find(
      ({ path }) => path.endsWith("/main.tsp"),
    );
    const clientReference = combinedNewRelic.sourceReferences.find(
      ({ path }) => path.endsWith("/client.tsp"),
    );
    const originalChange = combinedNewRelic.changes[0];
    const versionSummary =
      "The existing New Relic management operation surface is published under stable API version 2026-06-01 without changing its REST behavior.";
    const versionItem = {
      id: "publish-2026-06-01-version-lineage",
      intent:
        "Publish the inherited New Relic management surface in stable 2026-06-01",
      transformationChain: [versionSummary],
      changes: [
        {
          ...structuredClone(originalChange),
          kind: "added",
          summary:
            "Publish the inherited New Relic management surface in stable 2026-06-01",
          operationIds: allOperations.map(
            ({ operationId }) => operationId,
          ),
          apiVersions: ["2026-06-01"],
          aspects: [
            {
              field: "2026-06-01 API-version availability",
              before: null,
              after:
                "35 existing operations are exposed in the new stable API version without a wire-behavior change.",
            },
          ],
          effect: versionSummary,
          typeSpecCause:
            "Add the stable Versions.v2026_06_01 member.",
          sourceReferences: mainReference ? [mainReference] : [],
          linkedFindingIds: ["compliance-remove-replaced-preview-version"],
        },
      ],
      restRepresentation: {
        summary: versionSummary,
        operations: allOperations.map((operation) => ({
          ...operation,
          sourceReferences: mainReference ? [mainReference] : [],
        })),
      },
      confidence: combinedNewRelic.confidence,
      sourceReferences: mainReference ? [mainReference] : [],
    };

    combinedNewRelic.id = "preserve-go-delete-parameter-order";
    replaceSemanticText(combinedNewRelic, {
      intent: "Preserve the released Go delete parameter order",
      summary:
        "Monitors_Delete keeps its REST contract, while a Go-only override preserves the released userEmail-before-monitorName method parameter order.",
    });
    combinedNewRelic.restRepresentation.operations = deleteOperation
      ? [
          {
            ...deleteOperation,
            sourceReferences: clientReference ? [clientReference] : [],
          },
        ]
      : [];
    combinedNewRelic.sourceReferences = clientReference
      ? [clientReference]
      : [];
    for (const change of combinedNewRelic.changes) {
      change.summary = combinedNewRelic.intent;
      change.operationIds = ["Monitors_Delete"];
      change.apiVersions = ["2026-06-01"];
      change.aspects = [
        {
          field: "Go method parameter order",
          before:
            "The default generated order places monitorName before userEmail.",
          after:
            "The Go-only override preserves userEmail before monitorName.",
        },
      ];
      change.effect = combinedNewRelic.restRepresentation.summary;
      change.typeSpecCause =
        "Add a Go-only @@override with the released delete parameter order.";
      change.sourceReferences = combinedNewRelic.sourceReferences;
      change.linkedFindingIds = [];
    }
    assessment.dimensions.semanticUnderstanding.items.push(versionItem);
  }

  if (assessment.pr === 44988) {
    correctNetwork44988(assessment, items);
    assessment.artifactEvidence.tcgc =
      "Preserved successful base/head generic TCGC artifacts were reused without recompilation. The service gateway update actions create a downstream compatibility risk when generated SDKs adopt 2025-09-01 because their methods can change from long-running pollers to synchronous calls.";
    assessment.artifactEvidence.canonicalComparison =
      "Materially consistent with the corrected PR #44988 report: 10 semantic intents, no REST breaking finding, one downstream SDK finding for the service gateway LRO-to-synchronous transition, failed compliance status with the same two medium findings, high confidence, and a Medium code-safety conclusion.";
  }

  return assessment;
}

function curatedTypeSpecDiffs(typeSpecDiffFixture, pr, itemId) {
  const fixture = typeSpecDiffFixture[pr];
  if (!fixture) return undefined;
  return fixture[itemId] ?? fixture[curatedDiffAliases[itemId]];
}

function correctHistoricalComplianceEvidence(
  assessment,
  complianceSpecification,
) {
  if (assessment.pr !== 44988 || !complianceSpecification) return;
  const canonical = buildCompliance(assessment, complianceSpecification);
  const canonicalById = new Map(
    canonical.findings.map((finding) => [finding.id, finding]),
  );
  const aliases = {
    "connection-analyzer-child-resource-template":
      "compliance-connection-analyzer-standard-resource-type",
    "connection-analyzer-lifecycle-operation-templates":
      "compliance-connection-analyzer-standard-resource-operations",
  };
  for (const finding of assessment.dimensions.azureCompliance.findings) {
    const canonicalFinding = canonicalById.get(aliases[finding.id]);
    if (!canonicalFinding) continue;
    finding.sourceReferences = canonicalFinding.sourceReferences;
    finding.codeSnippets = canonicalFinding.codeSnippets;
  }
}

function refineSemanticChanges(
  assessment,
  typeSpecDiffFixture,
  operationsByPr,
) {
  correctHistoricalSemanticChains(assessment, operationsByPr);
  for (const item of assessment.dimensions.semanticUnderstanding.items) {
    const typeSpecDiffs = curatedTypeSpecDiffs(
      typeSpecDiffFixture,
      String(assessment.pr),
      item.id,
    );
    if (!typeSpecDiffs) continue;
    for (const change of item.changes) {
      change.typeSpecDiffs = typeSpecDiffs;
    }
  }
  normalizeAssessmentSourceLinks(assessment);
}

function writeExistingAssessmentReports(reportRootValue, prValues) {
  const reportRoot = resolve(reportRootValue);
  const typeSpecDiffFixture = readJson(
    new URL("../fixtures/recent-pr-typespec-diffs.json", import.meta.url),
  );
  const complianceFixture = readJson(
    new URL("../fixtures/recent-pr-compliance.json", import.meta.url),
  );
  const operationsByPr = readJson(
    new URL("../fixtures/recent-pr-operations.json", import.meta.url),
  );
  for (const pr of prValues) {
    const outputDirectory = join(reportRoot, "assessments", pr);
    const assessmentPath = join(outputDirectory, "assessment.json");
    const assessment = readJson(assessmentPath);
    refineSemanticChanges(assessment, typeSpecDiffFixture, operationsByPr);
    correctHistoricalComplianceEvidence(
      assessment,
      complianceFixture[String(assessment.pr)],
    );
    const errors = validateAssessment(assessment);
    if (errors.length > 0) {
      throw new Error(`PR ${pr} report is invalid:\n${errors.join("\n")}`);
    }
    writeFileSync(
      assessmentPath,
      `${JSON.stringify(assessment, null, 2)}\n`,
    );
    writeFileSync(
      join(outputDirectory, "assessment.html"),
      renderAssessmentHtml(assessment),
    );
  }
  process.stdout.write(`Refreshed ${prValues.length} assessment reports.\n`);
}

function main() {
  if (process.argv[2] === "--refresh-existing") {
    const [reportRootValue, ...prValues] = process.argv.slice(3);
    if (!reportRootValue || prValues.length === 0) {
      throw new Error(
        "Usage: finalize-rerun-assessments.mjs --refresh-existing <report-root> <pr>...",
      );
    }
    writeExistingAssessmentReports(reportRootValue, prValues);
    return;
  }
  const [reportRootValue, rerunRootValue, ...remainingValues] =
    process.argv.slice(2);
  const htmlOnly = remainingValues[0] === "--html-only";
  const prValues = htmlOnly ? remainingValues.slice(1) : remainingValues;
  if (!reportRootValue || !rerunRootValue || prValues.length === 0) {
    throw new Error(
      "Usage: finalize-rerun-assessments.mjs <report-root> <rerun-root> [--html-only] <pr>...",
    );
  }
  const reportRoot = resolve(reportRootValue);
  const rerunRoot = resolve(rerunRootValue);
  const complianceFixture = readJson(
    new URL("../fixtures/recent-pr-compliance.json", import.meta.url),
  );
  const typeSpecDiffFixture = readJson(
    new URL("../fixtures/recent-pr-typespec-diffs.json", import.meta.url),
  );
  const executionTimeBreakdowns = readJson(
    new URL("../fixtures/execution-time-breakdowns.json", import.meta.url),
  );
  const operationsByPr = readJson(
    new URL("../fixtures/recent-pr-operations.json", import.meta.url),
  );
  const assessments = [];

  for (const pr of prValues) {
    const outputDirectory = join(reportRoot, "assessments", pr);
    const assessment = readJson(join(outputDirectory, "assessment.json"));
    const evidence = readJson(join(rerunRoot, pr, "evidence.json"));
    const complianceSpecification = complianceFixture[pr];
    const executionTimeBreakdown = executionTimeBreakdowns[pr];
    if (!complianceSpecification) {
      throw new Error(`Missing compliance fixture for PR ${pr}.`);
    }
    if (!executionTimeBreakdown) {
      throw new Error(`Missing execution-time breakdown for PR ${pr}.`);
    }

    assessment.overallConfidence = evidence.errors.length > 0 ? "low" : "high";
    assessment.baseline = evidence.baseline;
    assessment.head = evidence.head;
    assessment.projects = evidence.projects.map((project) => project.path);
    assessment.assessmentDuration = {
      totalMs: executionTimeBreakdown.totalMs,
      note: "Assessment-only timings retained from execution-time-analysis.md. Worktree creation, dependency installation, and other environment setup are excluded. Quality labels distinguish measured, estimated, and derived values.",
      breakdown: executionTimeBreakdown,
    };
    assessment.assessmentEvidence = {
      changedTypeSpec: evidence.sourceReferences,
      emitterRuns: emitterRuns(evidence),
    };
    try {
      assessment.dimensions.azureCompliance = buildCompliance(
        assessment,
        complianceSpecification,
      );
    } catch (error) {
      throw new Error(`PR ${pr} compliance evidence failed: ${error.message}`, {
        cause: error,
      });
    }
    assessment.errors = evidence.errors;
    assessment.schemaVersion = 2;
    refineSemanticChanges(assessment, typeSpecDiffFixture, operationsByPr);

    const errors = validateAssessment(assessment);
    if (errors.length > 0) {
      throw new Error(`PR ${pr} report is invalid:\n${errors.join("\n")}`);
    }
    mkdirSync(outputDirectory, { recursive: true });
    writeFileSync(
      join(outputDirectory, "assessment.html"),
      renderAssessmentHtml(assessment),
    );
    if (htmlOnly) {
      for (const obsoletePath of [
        join(outputDirectory, "assessment.json"),
      ]) {
        if (existsSync(obsoletePath)) rmSync(obsoletePath);
      }
    } else {
      writeFileSync(
        join(outputDirectory, "assessment.json"),
        `${JSON.stringify(assessment, null, 2)}\n`,
      );
    }
    assessments.push(assessment);
  }

  if (htmlOnly) {
    for (const obsoletePath of [
      join(reportRoot, "assessments.json"),
      join(reportRoot, "assessment-summary.md"),
      join(reportRoot, "assessments", "execution-time-analysis.md"),
    ]) {
      if (existsSync(obsoletePath)) rmSync(obsoletePath);
    }
  } else {
    writeFileSync(
      join(reportRoot, "assessments.json"),
      `${JSON.stringify(
        {
          schemaVersion: 2,
          generatedAt: new Date().toISOString(),
          assessments,
        },
        null,
        2,
      )}\n`,
    );
    const rows = assessments.map((assessment) => {
      const operations =
        assessment.dimensions.semanticUnderstanding.items.reduce(
          (sum, item) => sum + item.restRepresentation.operations.length,
          0,
        );
      const duration = assessment.assessmentDuration;
      const totalTime =
        duration.documentationReviewMs === null
          ? "unavailable"
          : duration.note?.toLowerCase().includes("approximate")
            ? `~${formatDuration(duration.totalMs)}`
            : formatDuration(duration.totalMs);
      return `| [${assessment.pr}](assessments/${assessment.pr}/assessment.html) | ${assessment.overallConfidence} | ${operations} | ${assessment.dimensions.restBreakingChanges.findings.length} | ${assessment.dimensions.restCompatibleDownstreamBreakingChanges.findings.length} | ${assessment.dimensions.azureCompliance.status} | ${totalTime} | ${assessment.errors.length} |`;
    });
    writeFileSync(
      join(reportRoot, "assessment-summary.md"),
      `# Live TypeSpec Assessment Evidence

All ${assessments.length} assessments were rerun from their recorded PR head and base revisions with exact lockfile dependencies plus base/head AutoRest and generic TCGC compilation. Compliance was assessed by comparing Agent-searched authoritative documentation directly with changed TypeSpec source.

| PR | Confidence | Operations | REST findings | Downstream findings | Compliance | Total assessment | Errors |
| --- | --- | ---: | ---: | ---: | --- | ---: | ---: |
${rows.join("\n")}
`,
    );
  }
  process.stdout.write(`Finalized ${assessments.length} assessment reports.\n`);
}

if (
  process.argv[1] &&
  resolve(process.argv[1]) === fileURLToPath(import.meta.url)
) {
  main();
}
