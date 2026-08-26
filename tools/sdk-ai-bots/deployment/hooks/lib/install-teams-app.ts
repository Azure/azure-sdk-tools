import { execFileSync } from "child_process";

const GRAPH_BASE_URL = "https://graph.microsoft.com/v1.0";

type FetchLike = (
  input: string | URL | Request,
  init?: RequestInit,
) => Promise<Response>;

export interface InstallTeamsAppOptions {
  teamId: string;
  teamsAppExternalId: string;
  tenantId: string;
  expectedBotId?: string;
  expectedVersion?: string;
  verificationAttempts?: number;
  verificationDelayMs?: number;
  log?: (message: string) => void;
  fetchImpl?: FetchLike;
  getAccessToken?: (tenantId: string) => string;
}

interface TeamsAppSummary {
  id?: string;
  externalId?: string;
  displayName?: string;
}

interface TeamsAppInstallation {
  id?: string;
  teamsApp?: TeamsAppSummary;
  teamsAppDefinition?: {
    version?: string;
    bot?: { id?: string };
  };
}

export interface TeamsAppInstallationStatus {
  state: "not-installed" | "current" | "drifted";
  installationId?: string;
  catalogAppId?: string;
  installedBotId?: string;
  installedVersion?: string;
}

function getGraphAccessToken(tenantId: string): string {
  return execFileSync(
    "az",
    [
      "account",
      "get-access-token",
      "--tenant",
      tenantId,
      "--resource-type",
      "ms-graph",
      "--query",
      "accessToken",
      "--output",
      "tsv",
    ],
    { encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] },
  ).trim();
}

async function graphError(response: Response): Promise<string> {
  const text = await response.text();
  if (!text) return `HTTP ${response.status} ${response.statusText}`.trim();

  try {
    const payload = JSON.parse(text) as { error?: { code?: string; message?: string } };
    const code = payload.error?.code;
    const message = payload.error?.message;
    if (code || message) return [code, message].filter(Boolean).join(": ");
  } catch {
    // Fall back to the response text below.
  }

  return text.slice(0, 1_000);
}

function escapeODataString(value: string): string {
  return value.replace(/'/g, "''");
}

function teamAppsUrl(teamId: string): URL {
  const url = new URL(
    `${GRAPH_BASE_URL}/teams/${encodeURIComponent(teamId)}/installedApps`,
  );
  url.searchParams.set(
    "$expand",
    "teamsApp($select=id,externalId,displayName),teamsAppDefinition($expand=bot)",
  );
  url.searchParams.set("$select", "id");
  return url;
}

async function listTeamInstallations(
  teamId: string,
  headers: { Authorization: string },
  fetchImpl: FetchLike,
): Promise<TeamsAppInstallation[]> {
  const response = await fetchImpl(teamAppsUrl(teamId), { headers });
  if (!response.ok) {
    throw new Error(
      `Unable to inspect installed apps for team '${teamId}': ` +
        `${await graphError(response)}. The signed-in account needs ` +
        "TeamsAppInstallation.ReadWriteForTeam.",
    );
  }

  const payload = (await response.json()) as { value?: TeamsAppInstallation[] };
  return payload.value ?? [];
}

function installationStatus(
  installations: TeamsAppInstallation[],
  options: InstallTeamsAppOptions,
): TeamsAppInstallationStatus {
  const installation = installations.find(
    (item) =>
      item.teamsApp?.externalId?.toLowerCase() ===
      options.teamsAppExternalId.toLowerCase(),
  );
  if (!installation) return { state: "not-installed" };

  const installedBotId = installation.teamsAppDefinition?.bot?.id;
  const installedVersion = installation.teamsAppDefinition?.version;
  const botMatches =
    !options.expectedBotId ||
    installedBotId?.toLowerCase() === options.expectedBotId.toLowerCase();
  const versionMatches =
    !options.expectedVersion || installedVersion === options.expectedVersion;

  return {
    state: botMatches && versionMatches ? "current" : "drifted",
    installationId: installation.id,
    catalogAppId: installation.teamsApp?.id,
    installedBotId,
    installedVersion,
  };
}

async function waitForCurrentInstallation(
  options: InstallTeamsAppOptions,
  headers: { Authorization: string },
  fetchImpl: FetchLike,
): Promise<TeamsAppInstallationStatus> {
  const attempts = options.verificationAttempts ?? 6;
  const delayMs = options.verificationDelayMs ?? 5_000;
  let status: TeamsAppInstallationStatus = { state: "not-installed" };

  for (let attempt = 1; attempt <= attempts; attempt++) {
    status = installationStatus(
      await listTeamInstallations(options.teamId, headers, fetchImpl),
      options,
    );
    if (status.state === "current") return status;
    if (attempt < attempts && delayMs > 0) {
      await new Promise((resolve) => setTimeout(resolve, delayMs));
    }
  }

  return status;
}

export async function getTeamsAppInstallationStatus(
  options: InstallTeamsAppOptions,
): Promise<TeamsAppInstallationStatus> {
  const fetchImpl = options.fetchImpl ?? fetch;
  const getAccessToken = options.getAccessToken ?? getGraphAccessToken;
  const token = getAccessToken(options.tenantId);
  if (!token) {
    throw new Error(`Azure CLI returned an empty Microsoft Graph token for tenant '${options.tenantId}'.`);
  }

  const headers = { Authorization: `Bearer ${token}` };
  return installationStatus(
    await listTeamInstallations(options.teamId, headers, fetchImpl),
    options,
  );
}

export async function installTeamsAppInTeam(
  options: InstallTeamsAppOptions,
): Promise<"installed" | "already-installed" | "upgraded"> {
  const log = options.log ?? console.log;
  const fetchImpl = options.fetchImpl ?? fetch;
  const getAccessToken = options.getAccessToken ?? getGraphAccessToken;
  const token = getAccessToken(options.tenantId);

  if (!token) {
    throw new Error(`Azure CLI returned an empty Microsoft Graph token for tenant '${options.tenantId}'.`);
  }

  const headers = { Authorization: `Bearer ${token}` };
  const installations = await listTeamInstallations(
    options.teamId,
    headers,
    fetchImpl,
  );
  const current = installationStatus(installations, options);

  if (current.state === "current") {
    log(`Teams app '${options.teamsAppExternalId}' is already current in team '${options.teamId}'.`);
    return "already-installed";
  }

  if (current.state === "drifted") {
    if (!current.installationId) {
      throw new Error(
        `Installed Teams app '${options.teamsAppExternalId}' has no installation ID.`,
      );
    }

    const upgradeUrl =
      `${GRAPH_BASE_URL}/teams/${encodeURIComponent(options.teamId)}` +
      `/installedApps/${encodeURIComponent(current.installationId)}/upgrade`;
    const upgradeResponse = await fetchImpl(upgradeUrl, {
      method: "POST",
      headers,
    });
    if (!upgradeResponse.ok) {
      throw new Error(
        `Unable to upgrade Teams app '${options.teamsAppExternalId}' in team ` +
          `'${options.teamId}': ${await graphError(upgradeResponse)}. Publish and ` +
          `approve manifest version '${options.expectedVersion ?? "newer"}' before retrying.`,
      );
    }

    const verified = await waitForCurrentInstallation(options, headers, fetchImpl);
    if (verified.state !== "current") {
      throw new Error(
        `Teams app '${options.teamsAppExternalId}' upgrade did not converge: expected ` +
          `version '${options.expectedVersion ?? "any"}' and bot ` +
          `'${options.expectedBotId ?? "any"}', but found version ` +
          `'${verified.installedVersion ?? "unknown"}' and bot ` +
          `'${verified.installedBotId ?? "unknown"}'.`,
      );
    }

    log(`Upgraded Teams app '${options.teamsAppExternalId}' in team '${options.teamId}'.`);
    return "upgraded";
  }

  let catalogApp: TeamsAppSummary | undefined;

  if (!catalogApp?.id) {
    const catalogUrl = new URL(`${GRAPH_BASE_URL}/appCatalogs/teamsApps`);
    catalogUrl.searchParams.set(
      "$filter",
      `externalId eq '${escapeODataString(options.teamsAppExternalId)}'`,
    );
    catalogUrl.searchParams.set("$select", "id,externalId,displayName");

    const catalogResponse = await fetchImpl(catalogUrl, { headers });
    if (!catalogResponse.ok) {
      throw new Error(
        `Unable to resolve Teams app '${options.teamsAppExternalId}' in the tenant app catalog: ` +
          `${await graphError(catalogResponse)}. The signed-in account needs AppCatalog.Read.All ` +
          "for a first-time team installation.",
      );
    }

    const catalog = (await catalogResponse.json()) as { value?: TeamsAppSummary[] };
    catalogApp = catalog.value?.find(
      (app) => app.externalId?.toLowerCase() === options.teamsAppExternalId.toLowerCase(),
    );
  }

  if (!catalogApp?.id) {
    throw new Error(
      `Teams app '${options.teamsAppExternalId}' is not in the tenant app catalog. ` +
        "Publish and approve the app in Teams Admin Center before installing it into a team.",
    );
  }

  const installUrl =
    `${GRAPH_BASE_URL}/teams/${encodeURIComponent(options.teamId)}/installedApps`;
  const installResponse = await fetchImpl(installUrl, {
    method: "POST",
    headers: {
      ...headers,
      "Content-Type": "application/json",
    },
    body: JSON.stringify({
      "teamsApp@odata.bind":
        `${GRAPH_BASE_URL}/appCatalogs/teamsApps/${encodeURIComponent(catalogApp.id)}`,
    }),
  });

  if (installResponse.ok) {
    if (options.expectedBotId || options.expectedVersion) {
      const verified = await waitForCurrentInstallation(options, headers, fetchImpl);
      if (verified.state !== "current") {
        throw new Error(
          `Teams app '${options.teamsAppExternalId}' was installed but its definition ` +
            `does not match version '${options.expectedVersion ?? "any"}' and bot ` +
            `'${options.expectedBotId ?? "any"}'.`,
        );
      }
    }
    log(`Installed Teams app '${options.teamsAppExternalId}' into team '${options.teamId}'.`);
    return "installed";
  }

  const error = await graphError(installResponse);
  if (installResponse.status === 409 && /already\s+installed/i.test(error)) {
    log(`Teams app '${options.teamsAppExternalId}' is already installed in team '${options.teamId}'.`);
    return "already-installed";
  }

  throw new Error(
    `Unable to install Teams app '${options.teamsAppExternalId}' into team '${options.teamId}': ` +
      `${error}. The signed-in account needs TeamsAppInstallation.ReadWriteForTeam.`,
  );
}