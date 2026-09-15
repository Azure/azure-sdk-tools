import { parseArgs } from "node:util";
import { AzureCliCredential } from "@azure/identity";
import { readDeploymentTarget, validateDeploymentTarget } from "../lib/deployment-check.js";
import { readStorageConfig } from "../lib/storage-config.js";
import { createPublisherAuthorizer } from "../lib/publisher-auth.js";

const { values } = parseArgs({ options: { subscription: { type: "string" }, "resource-group": { type: "string" }, app: { type: "string" } } });
if (!values.subscription || !values["resource-group"] || !values.app) throw new Error("Provide --subscription, --resource-group and --app.");
const credential = new AzureCliCredential({ processTimeoutInMs: 30_000 });
const token = await credential.getToken("https://management.azure.com/.default");
const { site, authentication, endpoints, settings } = await readDeploymentTarget({ subscription: values.subscription,
  resourceGroup: values["resource-group"], app: values.app, token: token.token });
const env = settings.properties ?? {};
if (env.VALLY_STORAGE_PROVIDER !== "azure" || env.VALLY_LOCAL_POC === "true") throw new Error("Deployment refused: configure Azure storage and disable the local POC bypass.");
readStorageConfig(env, process.cwd());
createPublisherAuthorizer({ tenantId: env.VALLY_INGEST_TENANT_ID, audience: env.VALLY_INGEST_AUDIENCE,
  clientIds: (env.VALLY_INGEST_CLIENT_IDS ?? "").split(",").map((id) => id.trim()).filter(Boolean) });
if (!env.HOST || ["127.0.0.1", "localhost", "::1"].includes(env.HOST)) throw new Error("Deployment refused: configure the App Service bind host.");
console.log(JSON.stringify(validateDeploymentTarget(site, authentication, endpoints), null, 2));