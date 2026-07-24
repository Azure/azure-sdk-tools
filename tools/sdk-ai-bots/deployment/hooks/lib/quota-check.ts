/**
 * Reusable resource-quota verification helpers.
 *
 * Used by:
 *   - hooks/preprovision.ts   (fail-fast gate before `azd provision`)
 *   - scripts/check-quotas.ts (standalone CLI for on-demand checks)
 *
 * Each provider's `locations/{loc}/usages` endpoint returns a uniform
 * `{value: [{name, currentValue, limit}]}` shape, which we normalise below.
 */

import { execSync } from "child_process";

export type QuotaTarget = {
  provider: string;
  apiVersion: string;
  /**
   * Optional whitelist of `name.value` strings for the provider's usages
   * endpoint. When set, only matching entries are considered. Used to narrow
   * the report to just the quotas this deployment actually consumes.
   */
  filter?: (nameValue: string) => boolean;
};

export type UsageItem = {
  name?: { value?: string; localizedValue?: string };
  currentValue?: number;
  limit?: number;
};

export type ExhaustedQuota = {
  provider: string;
  label: string;
  nameValue: string;
  current: number;
  limit: number;
};

export type RegionEvaluation = {
  exhausted: ExhaustedQuota[];
  warnings: string[];
  unreachable: string[];
};

export type QuotaCheckResult = {
  ok: boolean;
  warnings: string[];
  unreachable: string[];
  exhausted: ExhaustedQuota[];
  alternatives: string[];
  /** Human-readable failure message; empty when `ok` is true. */
  message: string;
};

/**
 * Whitelist of quota keys this deployment actually consumes. Everything else
 * returned by the provider usage endpoints is filtered out so the report
 * stays focused. Keep these in sync with:
 *   - Microsoft.Storage:           infra/modules/qaBotSharedResources/sharedResources.bicep (storageAccount)
 *   - Microsoft.Search:            infra/modules/qaBotSharedResources/sharedResources.bicep (searchService SKU)
 *   - Microsoft.CognitiveServices: infra/modules/qaBotAgent/component.bicep (account kind/sku + model deployments)
 */
const CS_ACCOUNT_KIND = "AIServices"; // Microsoft.CognitiveServices/accounts.kind
const CS_ACCOUNT_SKU = "S0";
const CS_MODEL_SKU = "GlobalStandard";
const OPENAI_MODELS = [
  "gpt-4.1",
  "gpt-5.4",
  "gpt-5.1",
  "gpt-5-mini",
  "text-embedding-ada-002",
];

const STORAGE_QUOTAS = new Set(["StorageAccounts"]);
const SEARCH_QUOTAS = new Set(["standard"]); // sharedResources.bicep uses Standard SKU
const COGNITIVE_QUOTAS = new Set<string>([
  `${CS_ACCOUNT_KIND}.${CS_ACCOUNT_SKU}.AccountCount`,
  ...OPENAI_MODELS.map((m) => `OpenAI.${CS_MODEL_SKU}.${m}`),
]);

/** Providers whose usage APIs we poll. Order controls display order only. */
export const QUOTA_TARGETS: QuotaTarget[] = [
  {
    provider: "Microsoft.Storage",
    apiVersion: "2023-05-01",
    filter: (n) => STORAGE_QUOTAS.has(n),
  },
  {
    provider: "Microsoft.Search",
    apiVersion: "2023-11-01",
    filter: (n) => SEARCH_QUOTAS.has(n),
  },
  {
    provider: "Microsoft.CognitiveServices",
    apiVersion: "2023-05-01",
    filter: (n) => COGNITIVE_QUOTAS.has(n),
  },
];

/**
 * Curated fallback regions probed when the primary target is exhausted.
 * Ordered by proximity to westus2 and by broad service availability
 * (all three providers, including Cognitive Services / OpenAI).
 */
export const CANDIDATE_REGIONS = [
  "westus2",
  "westus3",
  "eastus",
  "eastus2",
  "centralus",
  "southcentralus",
  "northcentralus",
  "westeurope",
  "northeurope",
  "uksouth",
  "japaneast",
  "australiaeast",
  "southeastasia",
];

/** Call `az rest` for a provider's usages endpoint. Returns null on error. */
export function fetchUsages(
  subscriptionId: string,
  provider: string,
  region: string,
  apiVersion: string
): UsageItem[] | null {
  const url =
    `https://management.azure.com/subscriptions/${subscriptionId}` +
    `/providers/${provider}/locations/${region}/usages?api-version=${apiVersion}`;
  try {
    const raw = execSync(`az rest --method GET --url "${url}"`, {
      encoding: "utf8",
      stdio: ["ignore", "pipe", "ignore"],
    });
    const parsed = JSON.parse(raw) as { value?: UsageItem[] };
    return parsed.value ?? [];
  } catch {
    return null;
  }
}

/**
 * Poll every {@link QUOTA_TARGETS} entry for the given region and classify
 * each usage item as exhausted (>= 100%), warning (>= 80%), or fine.
 */
export function evaluateRegion(subscriptionId: string, region: string): RegionEvaluation {
  const exhausted: ExhaustedQuota[] = [];
  const warnings: string[] = [];
  const unreachable: string[] = [];

  for (const { provider, apiVersion, filter } of QUOTA_TARGETS) {
    const items = fetchUsages(subscriptionId, provider, region, apiVersion);
    if (items === null) {
      unreachable.push(provider);
      continue;
    }
    for (const item of items) {
      const nameValue = item.name?.value ?? "";
      if (filter && !filter(nameValue)) continue;
      const label = item.name?.localizedValue ?? (nameValue || "unknown");
      const current = Number(item.currentValue ?? 0);
      const limit = Number(item.limit ?? 0);
      if (limit <= 0) continue; // unlimited or N/A
      const util = current / limit;
      if (current >= limit) {
        exhausted.push({ provider, label, nameValue, current, limit });
      } else if (util >= 0.8) {
        warnings.push(
          `${provider} @ ${region} — ${label}: ${current}/${limit} ` +
            `(${Math.round(util * 100)}% used)`
        );
      }
    }
  }
  return { exhausted, warnings, unreachable };
}

/**
 * Given exhausted quotas in one region, probe {@link CANDIDATE_REGIONS} and
 * return the first `maxSuggestions` where every exhausted quota has headroom.
 * Only re-queries providers that were exhausted, to keep probe cost bounded.
 */
export function suggestAlternativeRegions(
  subscriptionId: string,
  originalExhausted: ExhaustedQuota[],
  excludeRegion: string,
  maxSuggestions = 3
): string[] {
  const exhaustedProviders = new Set(originalExhausted.map((e) => e.provider));
  const exhaustedKeys = new Set(
    originalExhausted.map((e) => `${e.provider}::${e.nameValue}`)
  );
  const candidates = CANDIDATE_REGIONS.filter((r) => r !== excludeRegion);
  const good: string[] = [];

  for (const region of candidates) {
    let ok = true;
    for (const { provider, apiVersion, filter } of QUOTA_TARGETS) {
      if (!exhaustedProviders.has(provider)) continue;
      const items = fetchUsages(subscriptionId, provider, region, apiVersion);
      if (items === null) {
        ok = false;
        break;
      }
      for (const item of items) {
        const nameValue = item.name?.value ?? "";
        if (filter && !filter(nameValue)) continue;
        const key = `${provider}::${nameValue}`;
        if (!exhaustedKeys.has(key)) continue;
        const current = Number(item.currentValue ?? 0);
        const limit = Number(item.limit ?? 0);
        if (limit <= 0 || current >= limit) {
          ok = false;
          break;
        }
      }
      if (!ok) break;
    }
    if (ok) good.push(region);
    if (good.length >= maxSuggestions) break;
  }
  return good;
}

/**
 * End-to-end orchestration: evaluate the target region, and if any quota is
 * exhausted, probe alternatives and assemble a human-readable message.
 * Never throws — callers decide how to react.
 */
export function runQuotaCheck(opts: {
  subscriptionId: string;
  location: string;
  envName?: string;
}): QuotaCheckResult {
  const { subscriptionId, location, envName } = opts;

  const { exhausted, warnings, unreachable } = evaluateRegion(subscriptionId, location);

  if (exhausted.length === 0) {
    return { ok: true, warnings, unreachable, exhausted, alternatives: [], message: "" };
  }

  const summary = exhausted
    .map((e) => `  ✗ ${e.provider} @ ${location} — ${e.label}: ${e.current}/${e.limit}`)
    .join("\n");

  const alternatives = suggestAlternativeRegions(subscriptionId, exhausted, location);
  const suggestion =
    alternatives.length > 0
      ? `\n\nRegions with headroom for the exhausted quotas: ${alternatives.join(", ")}.\n` +
        `To retarget, edit .environments.${envName || "<env>"}.regions[0].name in ` +
        `infra/environments/environment-suite.yaml, then rerun scripts/sync-env-suite.ps1.`
      : `\n\nNo alternative region among the probed candidates has headroom. ` +
        `Request a quota increase in the Azure portal (Subscription → Usage + quotas).`;

  const message =
    `One or more quotas are exhausted in '${location}':\n` + summary + suggestion;

  return { ok: false, warnings, unreachable, exhausted, alternatives, message };
}
