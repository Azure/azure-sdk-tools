import {
  type CommandRunner,
  runCommand,
} from "../../scripts/lib/cli.js";
import {
  buildAzdEnvironmentValues,
  loadEnvironmentSuite,
} from "./env-suite.js";

interface QuotaDemand {
  provider: string;
  apiVersion: string;
  region: string;
  nameValue: string;
  required: number;
}

export interface ExhaustedQuota extends QuotaDemand {
  label: string;
  current: number;
  limit: number;
}

export interface QuotaCheckResult {
  ok: boolean;
  warnings: string[];
  exhausted: ExhaustedQuota[];
  message: string;
}

export interface QuotaCheckOptions {
  subscriptionId: string;
  location: string;
  envName?: string;
  env?: NodeJS.ProcessEnv;
}

const MODEL_SETTINGS = [
  { name: "gpt-4.1", capacityKey: "GPT_4_1_CAPACITY", defaultCapacity: 1 },
  { name: "gpt-5.6-sol", capacityKey: "GPT_5_6_SOL_CAPACITY", defaultCapacity: 500 },
  { name: "gpt-5.1", capacityKey: "GPT_5_1_CAPACITY", defaultCapacity: 1 },
  { name: "gpt-5-mini", capacityKey: "GPT_5_MINI_CAPACITY", defaultCapacity: 1 },
  { name: "text-embedding-3-small", capacityKey: "TEXT_EMBEDDING_3_SMALL_CAPACITY", defaultCapacity: 1 },
];

function isRecord(value: unknown): value is Record<string, unknown> {
  return value !== null && typeof value === "object" && !Array.isArray(value);
}

function record(value: unknown, description: string): Record<string, unknown> {
  if (!isRecord(value)) throw new Error(`Invalid Azure response: ${description} must be an object.`);
  return value;
}

function capacity(value: unknown, description: string): number {
  if (typeof value !== "number" || !Number.isFinite(value) || value < 0) {
    throw new Error(`Cannot verify quota: ${description} must be a non-negative number.`);
  }
  return value;
}

function normalizedRegion(region: string): string {
  return region.toLowerCase().replaceAll(" ", "");
}

export function runQuotaCheck(
  options: QuotaCheckOptions,
  run: CommandRunner = runCommand,
): QuotaCheckResult {
  const defaults = options.envName
    ? buildAzdEnvironmentValues(loadEnvironmentSuite(), options.envName)
    : {};
  const env = { ...defaults, ...(options.env ?? process.env) };
  const resourceGroup = env.AZURE_RESOURCE_GROUP?.trim();
  if (!options.subscriptionId || !options.location || !resourceGroup) {
    throw new Error(
      "Quota verification requires a subscription, location, and AZURE_RESOURCE_GROUP. " +
        "Select an environment or supply its deployment values.",
    );
  }
  const aiLocation = env.AZURE_AI_LOCATION?.trim() || options.location;
  const baseUrl = `https://management.azure.com/subscriptions/${encodeURIComponent(options.subscriptionId)}`;
  const groupUrl = `${baseUrl}/resourceGroups/${encodeURIComponent(resourceGroup)}`;
  const warnings: string[] = [];
  const demands: QuotaDemand[] = [];

  function get(url: string): unknown {
    let raw: string;
    try {
      raw = run("az", ["rest", "--method", "GET", "--url", url, "--output", "json"], { capture: true });
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      if (/\b(ResourceNotFound|ResourceGroupNotFound|ParentResourceNotFound)\b/.test(message)) {
        return null;
      }
      throw new Error(`Cannot verify quota: reading ${url} failed: ${message}`, { cause: error });
    }
    try {
      const value: unknown = JSON.parse(raw);
      if (!isRecord(value)) throw new Error("Expected an ARM response object.");
      return value;
    } catch (error) {
      throw new Error(`Cannot verify quota: ${url} returned invalid JSON.`, { cause: error });
    }
  }

  function list(url: string, allowMissing = false): unknown[] {
    const items: unknown[] = [];
    let next: string | undefined = url;
    while (next) {
      const response = get(next);
      if (response === null && allowMissing) return items;
      const body = record(response, next);
      if (!Array.isArray(body.value)) {
        throw new Error(`Cannot verify quota: ${next} has no resource or usage list.`);
      }
      items.push(...body.value);
      if (body.nextLink !== undefined && typeof body.nextLink !== "string") {
        throw new Error(`Cannot verify quota: ${next} returned an invalid nextLink.`);
      }
      next = body.nextLink;
      if (next && new URL(next).origin !== "https://management.azure.com") {
        throw new Error("Cannot verify quota: Azure returned a nextLink outside the ARM endpoint.");
      }
    }
    return items;
  }

  let groupResources: unknown[] | undefined;
  function resource(
    resourceType: string,
    nameKey: string,
    apiVersion: string,
  ): { name: string; body: Record<string, unknown> } | null {
    const override = env[`${nameKey}_OVERRIDE`];
    const name = (override ?? env[nameKey])?.trim();
    if (!name) {
      groupResources ??= list(`${groupUrl}/resources?api-version=2021-04-01`, true);
      if (groupResources.some((item) => {
        const type = record(item, "resource").type;
        if (typeof type !== "string") {
          throw new Error("Cannot verify existing allocation: a resource has no type.");
        }
        return type.toLowerCase() === resourceType.toLowerCase();
      })) {
        throw new Error(
          `Cannot verify existing allocation for ${resourceType}: ${nameKey}_OVERRIDE ` +
            "is unresolved and the resource group already contains this resource type.",
        );
      }
      return null;
    }
    const body = get(`${groupUrl}/providers/${resourceType}/${encodeURIComponent(name)}?api-version=${apiVersion}`);
    return body === null ? null : { name, body: record(body, name) };
  }

  function reusable(
    existing: ReturnType<typeof resource>,
    region: string,
    sku?: string,
    kind?: string,
  ): boolean {
    if (!existing) return false;
    if (typeof existing.body.location !== "string") {
      throw new Error(`Cannot verify quota: ${existing.name} has no location.`);
    }
    if (normalizedRegion(existing.body.location) !== normalizedRegion(region)) return false;
    if (sku) {
      const existingSku = record(existing.body.sku, `${existing.name} SKU`).name;
      if (typeof existingSku !== "string") {
        throw new Error(`Cannot verify quota: ${existing.name} has no SKU name.`);
      }
      if (existingSku !== sku) return false;
    }
    if (kind) {
      if (typeof existing.body.kind !== "string") {
        throw new Error(`Cannot verify quota: ${existing.name} has no account kind.`);
      }
      if (existing.body.kind !== kind) return false;
    }
    return true;
  }

  const storage = resource("Microsoft.Storage/storageAccounts", "STORAGE_ACCOUNT_NAME", "2023-05-01");
  demands.push({
    provider: "Microsoft.Storage", apiVersion: "2023-05-01",
    region: options.location, nameValue: "StorageAccounts",
    required: reusable(storage, options.location) ? 0 : 1,
  });
  const search = resource("Microsoft.Search/searchServices", "SEARCH_SERVICE_NAME", "2023-11-01");
  demands.push({
    provider: "Microsoft.Search", apiVersion: "2023-11-01",
    region: options.location, nameValue: "standard",
    required: reusable(search, options.location, "standard") ? 0 : 1,
  });
  const account = resource("Microsoft.CognitiveServices/accounts", "AI_RESOURCE_NAME", "2023-05-01");
  const reuseAccount = reusable(account, aiLocation, "S0", "AIServices");
  demands.push({
    provider: "Microsoft.CognitiveServices", apiVersion: "2023-05-01",
    region: aiLocation, nameValue: "AIServices.S0.AccountCount",
    required: reuseAccount ? 0 : 1,
  });
  const deployments = reuseAccount && account
    ? list(`${groupUrl}/providers/Microsoft.CognitiveServices/accounts/${encodeURIComponent(account.name)}/deployments?api-version=2023-05-01`)
        .map((item) => record(item, "model deployment"))
    : [];

  for (const model of MODEL_SETTINGS) {
    const rawCapacity = env[model.capacityKey] ?? String(model.defaultCapacity);
    const desired = Number(rawCapacity);
    if (!rawCapacity.trim() || !Number.isInteger(desired) || desired < 0) {
      throw new Error(`${model.capacityKey} must be a non-negative integer.`);
    }
    const sku = model.name === "gpt-4.1"
      ? (env.GPT_4_1_SKU_NAME ?? "GlobalStandard")
      : "GlobalStandard";
    const existing = deployments.find((deployment) => deployment.name === model.name);
    let allocated = 0;
    if (existing) {
      const existingModel = record(record(existing.properties, "deployment properties").model, "deployment model");
      const existingSku = record(existing.sku, "deployment SKU");
      if (typeof existingModel.name !== "string" || typeof existingSku.name !== "string") {
        throw new Error(`Cannot verify quota: ${model.name} has no model or SKU name.`);
      }
      if (existingModel.name === model.name && existingSku.name === sku) {
        allocated = capacity(existingSku.capacity, `${model.name} existing capacity`);
      }
    }
    demands.push({
      provider: "Microsoft.CognitiveServices", apiVersion: "2023-05-01",
      region: aiLocation, nameValue: `OpenAI.${sku}.${model.name}`,
      required: Math.max(0, desired - allocated),
    });
  }

  const usageLists = new Map<string, unknown[]>();
  const exhausted: ExhaustedQuota[] = [];
  for (const demand of demands) {
    if (demand.required === 0) continue;
    const url = `${baseUrl}/providers/${demand.provider}/locations/${encodeURIComponent(demand.region)}/usages?api-version=${demand.apiVersion}`;
    let usages = usageLists.get(url);
    if (!usages) {
      usages = list(url);
      usageLists.set(url, usages);
    }
    const usage = usages.find((item) =>
      isRecord(item) && isRecord(item.name) && item.name.value === demand.nameValue);
    if (!isRecord(usage) || !isRecord(usage.name)) {
      throw new Error(
        `Cannot verify quota: ${demand.provider} @ ${demand.region} did not return ${demand.nameValue}.`,
      );
    }
    const current = capacity(usage.currentValue, `${demand.nameValue} current usage`);
    const limit = capacity(usage.limit, `${demand.nameValue} quota limit`);
    const label = typeof usage.name.localizedValue === "string"
      ? usage.name.localizedValue : demand.nameValue;
    if (current + demand.required > limit) {
      exhausted.push({ ...demand, current, limit, label });
    } else if (limit > 0 && (current + demand.required) / limit >= 0.8) {
      warnings.push(
        `${demand.provider} @ ${demand.region} — ${label}: ` +
          `${current}/${limit} used; additional allocation ${demand.required}.`,
      );
    }
  }
  const message = exhausted.length === 0 ? "" :
    "Insufficient quota for additional deployment allocations:\n" +
    exhausted.map((item) =>
      `  ${item.provider} @ ${item.region} — ${item.label}: ` +
      `${item.current}/${item.limit} used; additional allocation ${item.required}; ` +
      `available ${Math.max(0, item.limit - item.current)}.`,
    ).join("\n") +
    "\nRequest quota in the listed regions or reduce the planned allocation increases.";
  return { ok: exhausted.length === 0, warnings, exhausted, message };
}
