import assert from "node:assert/strict";
import test from "node:test";

import { installTeamsAppInTeam } from "../hooks/lib/install-teams-app.js";

test("resolves the catalog app and installs it into the configured team", async () => {
  const requests: Array<{ url: string; init?: RequestInit }> = [];
  const fetchImpl = async (
    input: string | URL | Request,
    init?: RequestInit,
  ): Promise<Response> => {
    const url = String(input);
    requests.push({ url, init });
    if (requests.length === 1) {
      return new Response(JSON.stringify({ value: [] }), {
        status: 200,
        headers: { "Content-Type": "application/json" },
      });
    }
    if (requests.length === 2) {
      return new Response(
        JSON.stringify({
          value: [{ id: "catalog-id", externalId: "manifest-id", displayName: "QA Bot" }],
        }),
        { status: 200, headers: { "Content-Type": "application/json" } },
      );
    }
    return new Response(null, { status: 200 });
  };

  const result = await installTeamsAppInTeam({
    teamId: "team-id",
    teamsAppExternalId: "manifest-id",
    tenantId: "tenant-id",
    getAccessToken: () => "token",
    fetchImpl,
    log: () => undefined,
  });

  assert.equal(result, "installed");
  assert.equal(requests.length, 3);

  const teamAppsUrl = new URL(requests[0].url);
  assert.equal(teamAppsUrl.pathname, "/v1.0/teams/team-id/installedApps");
  assert.equal(
    teamAppsUrl.searchParams.get("$expand"),
    "teamsApp($select=id,externalId,displayName),teamsAppDefinition($expand=bot)",
  );

  const catalogUrl = new URL(requests[1].url);
  assert.equal(catalogUrl.pathname, "/v1.0/appCatalogs/teamsApps");
  assert.equal(catalogUrl.searchParams.get("$filter"), "externalId eq 'manifest-id'");
  assert.equal(requests[1].init?.headers instanceof Headers, false);
  assert.deepEqual(requests[1].init?.headers, { Authorization: "Bearer token" });

  assert.equal(
    requests[2].url,
    "https://graph.microsoft.com/v1.0/teams/team-id/installedApps",
  );
  assert.equal(requests[2].init?.method, "POST");
  assert.deepEqual(JSON.parse(String(requests[2].init?.body)), {
    "teamsApp@odata.bind":
      "https://graph.microsoft.com/v1.0/appCatalogs/teamsApps/catalog-id",
  });
});

test("reports when the app has not been published to the tenant catalog", async () => {
  let requestCount = 0;
  await assert.rejects(
    installTeamsAppInTeam({
      teamId: "team-id",
      teamsAppExternalId: "manifest-id",
      tenantId: "tenant-id",
      getAccessToken: () => "token",
      fetchImpl: async () => {
        requestCount++;
        return new Response(JSON.stringify({ value: [] }), {
          status: 200,
          headers: { "Content-Type": "application/json" },
        });
      },
      log: () => undefined,
    }),
    /not in the tenant app catalog/,
  );
  assert.equal(requestCount, 2);
});

test("leaves a current team installation unchanged", async () => {
  const requests: Array<{ url: string; init?: RequestInit }> = [];
  const result = await installTeamsAppInTeam({
    teamId: "team-id",
    teamsAppExternalId: "manifest-id",
    tenantId: "tenant-id",
    getAccessToken: () => "token",
    fetchImpl: async (input, init) => {
      requests.push({ url: String(input), init });
      if (requests.length === 1) {
        return new Response(
          JSON.stringify({
            value: [{
              id: "installation-id",
              teamsApp: { id: "installed-catalog-id", externalId: "manifest-id" },
              teamsAppDefinition: { version: "1.0.4", bot: { id: "bot-id" } },
            }],
          }),
          { status: 200, headers: { "Content-Type": "application/json" } },
        );
      }
      throw new Error("Unexpected request");
    },
    expectedBotId: "bot-id",
    expectedVersion: "1.0.4",
    log: () => undefined,
  });

  assert.equal(result, "already-installed");
  assert.equal(requests.length, 1);
});

test("upgrades and verifies a stale bot identity", async () => {
  const requests: Array<{ url: string; init?: RequestInit }> = [];
  const staleInstallation = {
    value: [{
      id: "installation-id",
      teamsApp: { id: "catalog-id", externalId: "manifest-id" },
      teamsAppDefinition: { version: "1.0.3", bot: { id: "old-bot-id" } },
    }],
  };
  const currentInstallation = {
    value: [{
      id: "installation-id",
      teamsApp: { id: "catalog-id", externalId: "manifest-id" },
      teamsAppDefinition: { version: "1.0.4", bot: { id: "new-bot-id" } },
    }],
  };

  const result = await installTeamsAppInTeam({
    teamId: "team-id",
    teamsAppExternalId: "manifest-id",
    tenantId: "tenant-id",
    expectedBotId: "new-bot-id",
    expectedVersion: "1.0.4",
    verificationDelayMs: 0,
    getAccessToken: () => "token",
    fetchImpl: async (input, init) => {
      requests.push({ url: String(input), init });
      if (requests.length === 1) {
        return new Response(JSON.stringify(staleInstallation), {
          status: 200,
          headers: { "Content-Type": "application/json" },
        });
      }
      if (requests.length === 2) return new Response(null, { status: 204 });
      return new Response(JSON.stringify(currentInstallation), {
        status: 200,
        headers: { "Content-Type": "application/json" },
      });
    },
    log: () => undefined,
  });

  assert.equal(result, "upgraded");
  assert.equal(requests.length, 3);
  assert.equal(
    requests[1].url,
    "https://graph.microsoft.com/v1.0/teams/team-id/installedApps/installation-id/upgrade",
  );
  assert.equal(requests[1].init?.method, "POST");
});

test("reports when a stale installation has no approved upgrade", async () => {
  let requestCount = 0;
  await assert.rejects(
    installTeamsAppInTeam({
      teamId: "team-id",
      teamsAppExternalId: "manifest-id",
      tenantId: "tenant-id",
      expectedBotId: "new-bot-id",
      expectedVersion: "1.0.4",
      getAccessToken: () => "token",
      fetchImpl: async () => {
        requestCount++;
        if (requestCount === 1) {
          return new Response(JSON.stringify({
            value: [{
              id: "installation-id",
              teamsApp: { id: "catalog-id", externalId: "manifest-id" },
              teamsAppDefinition: { version: "1.0.3", bot: { id: "old-bot-id" } },
            }],
          }), {
            status: 200,
            headers: { "Content-Type": "application/json" },
          });
        }
        return new Response(JSON.stringify({
          error: { code: "BadRequest", message: "No upgrade is available." },
        }), {
          status: 400,
          headers: { "Content-Type": "application/json" },
        });
      },
      log: () => undefined,
    }),
    /Publish and approve manifest version '1\.0\.4'/,
  );
});