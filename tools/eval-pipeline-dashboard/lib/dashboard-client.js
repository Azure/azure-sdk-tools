import { createReadStream } from "node:fs";
import { mkdtemp, rm, stat } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { createDashboardBundle, MAX_BUNDLE_BYTES } from "./dashboard-bundle.js";
import { fileHash } from "./submission-store.js";
import { isLoopback } from "./publisher-auth.js";
import { setTimeout as delay } from "node:timers/promises";

function endpoint(dashboardUrl) {
  const url = new URL(dashboardUrl);
  if (url.username || url.password || url.search || url.hash || (url.protocol !== "https:" && !(url.protocol === "http:" && isLoopback(url.hostname)))) {
    throw new Error("Dashboard URL must use HTTPS, or HTTP on loopback for the local POC.");
  }
  return url;
}

function retryDelay(response, attempt) {
  const retryAfter = response?.headers.get("retry-after");
  const seconds = retryAfter && /^\d+$/.test(retryAfter) ? Number(retryAfter) : null;
  const dateDelay = retryAfter && seconds === null ? Date.parse(retryAfter) - Date.now() : NaN;
  return Math.min(60_000, Math.max(0, seconds !== null ? seconds * 1000 : Number.isFinite(dateDelay) ? dateDelay : 1000 * 2 ** attempt));
}

export async function submitDashboardBundle({ bundlePath, dashboardUrl, accessToken, getAccessToken,
  fetchImpl = fetch, maxAttempts = 4, wait = delay }) {
  const url = endpoint(dashboardUrl);
  if (!Number.isSafeInteger(maxAttempts) || maxAttempts < 1 || maxAttempts > 10) throw new Error("maxAttempts must be between 1 and 10.");
  const info = await stat(bundlePath);
  if (info.size > MAX_BUNDLE_BYTES) throw new Error("Bundle exceeds the upload limit.");
  const headers = {
    "content-type": "application/zip",
    "content-length": String(info.size),
    "x-content-sha256": await fileHash(bundlePath),
  };
  for (let attempt = 0; attempt < maxAttempts; attempt++) {
    // Open a fresh stream for the SAME saved ZIP, never repackage on a retry.
    const body = createReadStream(bundlePath);
    let response;
    try {
      const token = getAccessToken ? await getAccessToken() : accessToken;
      if (token) headers.authorization = `Bearer ${token}`;
      response = await fetchImpl(new URL("/api/ingestions", url), {
        method: "POST", headers, body, duplex: "half", redirect: "error", signal: AbortSignal.timeout(60_000),
      });
    } catch (error) {
      if (attempt + 1 === maxAttempts) throw new Error("Dashboard submission failed after transport retries; keep the saved ZIP for retry.", { cause: error });
    } finally { body.destroy(); }
    if (response?.ok) {
      const result = await response.json();
        if (![200, 202].includes(response.status) || !/^[a-f0-9]{64}$/.test(result.id) || result.statusUrl !== `/api/ingestions/${result.id}` ||
          !["queued", "processing", "succeeded", "failed"].includes(result.status)) {
        throw new Error("Dashboard returned an invalid acceptance receipt.");
      }
      return result;
    }
    if (response && (![408, 429, 500, 502, 503, 504].includes(response.status) || attempt + 1 === maxAttempts)) {
      const result = await response.json().catch(() => ({}));
      throw new Error(`Dashboard returned ${response.status}: ${result.error?.code ?? "submission_failed"}`);
    }
    await response?.body?.cancel();
    await wait(retryDelay(response, attempt));
  }
}

export async function waitForReceipt({ receipt, dashboardUrl, accessToken, getAccessToken, fetchImpl = fetch,
  timeoutMs = 300_000, wait = delay, now = Date.now }) {
  const url = endpoint(dashboardUrl);
  if (!/^[a-f0-9]{64}$/.test(receipt.id)) throw new Error("Invalid receipt identity.");
  const deadline = now() + timeoutMs;
  let current = receipt;
  while (["queued", "processing"].includes(current.status)) {
    if (now() >= deadline) throw new Error(`Timed out waiting for receipt ${receipt.id}; acceptance remains durable.`);
    await wait(1000);
    const token = getAccessToken ? await getAccessToken() : accessToken;
    const response = await fetchImpl(new URL(`/api/ingestions/${receipt.id}`, url), {
      headers: token ? { authorization: `Bearer ${token}` } : {}, redirect: "error", signal: AbortSignal.timeout(30_000),
    });
    if (!response.ok) throw new Error(`Receipt lookup returned ${response.status}.`);
    current = await response.json();
    if (current.id !== receipt.id || !["queued", "processing", "succeeded", "failed", "expired"].includes(current.status)) {
      throw new Error("Dashboard returned an invalid receipt status.");
    }
  }
  if (current.status !== "succeeded") throw new Error(`Submission ${receipt.id} ${current.status}: ${current.errorCode ?? "not_succeeded"}`);
  return current;
}

export async function submitPipelineBundle({ inputDirectory, manifest, dashboardUrl, accessToken }) {
  const temporary = await mkdtemp(join(tmpdir(), "vally-submit-"));
  try {
    const bundlePath = join(temporary, "dashboard-bundle.zip");
    await createDashboardBundle({ inputDirectory, outputPath: bundlePath, manifest });
    return await submitDashboardBundle({ bundlePath, dashboardUrl, accessToken });
  } finally {
    await rm(temporary, { recursive: true, force: true });
  }
}