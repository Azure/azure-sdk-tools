import assert from "node:assert/strict";
import test from "node:test";

import { resolveAgentTargetImage } from "../hooks/lib/resolve-agent-image.js";

test("derives the native azd agent image from deployment inputs", () => {
  assert.equal(
    resolveAgentTargetImage({
      AZURE_CONTAINER_REGISTRY_ENDPOINT: "https://qabot.azurecr.io/",
      AZURE_ENV_NAME: "preview",
      AGENT_IMAGE_REPOSITORY: "sdk-ai-bots/agent-preview",
      AZD_IMAGE_TAG: "build-123",
    }),
    "qabot.azurecr.io/sdk-ai-bots/agent-preview:build-123",
  );
});

test("requires an independently selected target image", () => {
  assert.equal(
    resolveAgentTargetImage({
      AZURE_CONTAINER_REGISTRY_ENDPOINT: "qabot.azurecr.io",
      AZURE_ENV_NAME: "dev",
      AGENT_IMAGE_REPOSITORY: "sdk-ai-bots/agent-dev",
    }),
    undefined,
  );
});