import { execFileSync } from "child_process";

import { ensureServerAppAuthorization } from "./ensure-entra-app.js";

type Log = (message: string) => void;

function resolveServicePrincipalId(clientId: string, log: Log): string | undefined {
  try {
    return execFileSync(
      "az",
      ["ad", "sp", "show", "--id", clientId, "--query", "id", "--output", "tsv"],
      { encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] },
    ).trim() || undefined;
  } catch {
    log(`Unable to resolve service principal for managed identity client ID ${clientId}.`);
    return undefined;
  }
}

export function ensureAgentServerAuthorization(
  env: NodeJS.ProcessEnv = process.env,
  log: Log = console.log,
): void {
  const appId = env.SERVER_AUDIENCE?.trim();
  if (!appId) {
    log("SERVER_AUDIENCE not set - skipping server app authorization.");
    return;
  }

  const sharedPrincipalId = env.MANAGED_IDENTITY_PRINCIPAL_ID?.trim();
  const frontendClientId = env.BOT_MANAGED_IDENTITY_CLIENT_ID?.trim();
  const frontendPrincipalId = frontendClientId
    ? resolveServicePrincipalId(frontendClientId, log)
    : undefined;

  ensureServerAppAuthorization({
    appId,
    managedIdentityPrincipalIds: [sharedPrincipalId, frontendPrincipalId].filter(
      (principalId): principalId is string => !!principalId,
    ),
    preAuthorizeClientIds: frontendClientId ? [frontendClientId] : [],
  });
}