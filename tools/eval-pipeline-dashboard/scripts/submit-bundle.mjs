import { resolve } from "node:path";
import { submitDashboardBundle } from "../lib/dashboard-client.js";

if (!process.argv[2]) throw new Error("Usage: npm run poc:submit -- <dashboard-bundle.zip> [dashboard URL]");
const receipt = await submitDashboardBundle({
  bundlePath: resolve(process.argv[2]),
  dashboardUrl: process.argv[3] || process.env.POC_DASHBOARD_URL || "http://127.0.0.1:3201",
  accessToken: process.env.DASHBOARD_ACCESS_TOKEN,
});
console.log(JSON.stringify(receipt, null, 2));