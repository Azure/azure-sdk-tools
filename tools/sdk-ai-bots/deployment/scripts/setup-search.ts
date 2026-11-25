import { reconcileSearchResources } from "../hooks/lib/setup-search.js";

function required(key: string): string {
  const value = process.env[key]?.trim();
  if (!value) throw new Error(`${key} is required.`);
  return value;
}

const args = new Set(process.argv.slice(2));
const unknownArgs = [...args].filter((arg) => arg !== "--apply" && arg !== "--dry-run");
if (unknownArgs.length > 0 || (args.has("--apply") && args.has("--dry-run"))) {
  throw new Error("Usage: npm run setup-search -- [--dry-run | --apply]");
}

const dryRun = !args.has("--apply");

reconcileSearchResources({
  subscriptionId: required("AZURE_SUBSCRIPTION_ID"),
  resourceGroup: required("AZURE_RESOURCE_GROUP"),
  searchServiceName: required("SEARCH_SERVICE_NAME"),
  storageAccountName: required("STORAGE_ACCOUNT_NAME"),
  aiResourceName: required("AI_RESOURCE_NAME"),
  env: process.env,
  dryRun,
}).catch((error) => {
  console.error(`[setup-search] FAILED: ${error instanceof Error ? error.message : error}`);
  process.exit(1);
});