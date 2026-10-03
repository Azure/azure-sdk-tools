import assert from "node:assert/strict";
import test from "node:test";

import { isManagedApiConnection } from "../hooks/lib/managed-api-connection.js";

test("ignores missing API IDs from generic resource listings", () => {
  assert.equal(isManagedApiConnection(null, "teams"), false);
  assert.equal(isManagedApiConnection(undefined, "teams"), false);
});

test("matches a detailed managed API resource ID", () => {
  assert.equal(
    isManagedApiConnection(
      "/subscriptions/sub/providers/Microsoft.Web/locations/centralus/managedApis/teams",
      "teams",
    ),
    true,
  );
  assert.equal(
    isManagedApiConnection(
      "/subscriptions/sub/providers/Microsoft.Web/locations/centralus/managedApis/azureblob",
      "teams",
    ),
    false,
  );
});