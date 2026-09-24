import { parseArgs } from "node:util";
import { mkdir, writeFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { submitDashboardBundle, waitForReceipt } from "../lib/dashboard-client.js";
import { AzureCliCredential } from "@azure/identity";

const { values } = parseArgs({ options: { bundle: { type: "string" }, url: { type: "string" }, audience: { type: "string" },
  receipt: { type: "string" }, wait: { type: "boolean", default: false } } });
if (!values.bundle || !values.url || !values.receipt) throw new Error("Usage: publish-bundle.mjs --bundle <saved.zip> --url <dashboard> --receipt <receipt.json> [--audience <API app ID>] [--wait]");
const credential = values.audience ? new AzureCliCredential({ processTimeoutInMs: 30_000 }) : null;
const getAccessToken = values.audience ? async () => {
  // AzureCLI@2 provides the workload-identity session. Never log the token or
  // forward the credential error object (which may include command details).
  try {
    const token = (await credential.getToken(`${values.audience.replace(/\/$/, "")}/.default`))?.token;
    if (!token) throw new Error("No token returned.");
    return token;
  } catch { throw new Error("Unable to obtain the dashboard workload-identity token from the Azure CLI session."); }
} : undefined;
const options = { bundlePath: resolve(values.bundle), dashboardUrl: values.url, accessToken: process.env.DASHBOARD_ACCESS_TOKEN, getAccessToken };
let receipt = await submitDashboardBundle(options);
const output = resolve(values.receipt);
await mkdir(dirname(output), { recursive: true });
await writeFile(output, JSON.stringify(receipt, null, 2) + "\n");
console.log(`Dashboard accepted submission ${receipt.id}: ${new URL(receipt.statusUrl, values.url).href}`);
if (values.wait) {
  receipt = await waitForReceipt({ ...options, receipt });
  await writeFile(output, JSON.stringify(receipt, null, 2) + "\n");
  console.log(`Dashboard ingestion succeeded: ${receipt.id}`);
}