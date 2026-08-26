import assert from "node:assert/strict";
import test from "node:test";

import { getBotSignInAudience } from "../hooks/lib/ensure-entra-app.js";

test("uses a multitenant app when Azure and Teams tenants differ", () => {
  assert.equal(
    getBotSignInAudience("azure-tenant", "teams-tenant"),
    "AzureADMultipleOrgs",
  );
});

test("uses a single-tenant app when Azure and Teams tenants match", () => {
  assert.equal(
    getBotSignInAudience("same-tenant", "same-tenant"),
    "AzureADMyOrg",
  );
});

test("compares tenant IDs case-insensitively after trimming", () => {
  assert.equal(
    getBotSignInAudience(" Tenant-ABC ", "tenant-abc"),
    "AzureADMyOrg",
  );
});

test("defaults to a single-tenant app when the Teams tenant is unset", () => {
  assert.equal(getBotSignInAudience("azure-tenant", undefined), "AzureADMyOrg");
});