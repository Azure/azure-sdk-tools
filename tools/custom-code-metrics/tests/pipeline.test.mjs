import { test } from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

const pipeline = await readFile(new URL("../nightly.yml", import.meta.url), "utf8");

test("central schedule has no PR/resource triggers and requires trusted internal tools main", () => {
  assert.match(pipeline, /^trigger: none\r?\npr: none/);
  const condition = pipeline.match(/^\s+condition: (.*)$/m)?.[1];
  for (const requirement of [
    "eq(variables['System.TeamProject'], 'internal')",
    "eq(variables['Build.SourceBranch'], 'refs/heads/main')",
    "eq(variables['Build.Repository.Name'], 'Azure/azure-sdk-tools')",
    "in(variables['Build.Reason'], 'Schedule', 'Manual')",
  ]) assert.ok(condition?.includes(requirement), `Missing publication gate: ${requirement}`);
  assert.match(pipeline, /cron: '0 8 \* \* \*'/);
  assert.match(pipeline, /branches:\s+include:\s+- main/);
});
test("only the .NET adapter is wired, with an explicit read-only endpoint prerequisite and immutable per-run revision", () => {
  assert.match(pipeline, /default: SET_READ_ONLY_GITHUB_SERVICE_CONNECTION/);
  assert.match(pipeline, /repository: dotnet\s+type: github\s+name: Azure\/azure-sdk-for-net/);
  assert.match(pipeline, /endpoint: \$\{\{ parameters\.DotNetGitHubServiceConnection \}\}/);
  assert.match(pipeline, /ref: refs\/heads\/main\s+trigger: none/);
  assert.match(pipeline, /\$\[ resources\.repositories\.dotnet\.version \]/);
  assert.match(pipeline, /-ExpectedNetCommit "\$\(MetricsNetCommit\)"/);
  assert.match(pipeline, /-ExpectedToolsCommit "\$\(Build\.SourceVersion\)"/);
  assert.doesNotMatch(pipeline, /azure-sdk-for-(java|js|python|go)/);
});
test("nightly checkout paths are explicit, credentials are not persisted and publication follows validation and artifact retention", () => {
  assert.match(pipeline, /checkout: self\s+clean: true\s+fetchDepth: 1\s+path: s\/azure-sdk-tools\s+persistCredentials: false/);
  assert.match(pipeline, /checkout: dotnet\s+clean: true\s+fetchDepth: 1\s+path: s\/azure-sdk-for-net\s+persistCredentials: false/);
  const steps = [
    "Initialize-NightlyMetrics.ps1", "task: UseDotNet@2", "customCommand: run check",
    "customCommand: run test:publishing", "Collect-DotNetMetrics.ps1",
    "task: 1ES.PublishPipelineArtifact@1", "task: AzureCLI@2", "Publish-Metrics.ps1",
  ].map((name) => {
    const position = pipeline.indexOf(name);
    assert.ok(position >= 0, `Missing nightly step: ${name}`);
    return position;
  });
  assert.deepEqual(steps, [...steps].sort((a, b) => a - b));
  assert.match(pipeline, /version: \$\(MetricsDotNetSdkVersion\)/);
  assert.match(pipeline, /azureSubscription: azure-sdk-playground-custom-code-metrics/);
  assert.match(pipeline, /scriptLocation: scriptPath/);
  assert.doesNotMatch(pipeline, /inlineScript:|build:dashboard|Deploy-Dashboard|Deploy-Infrastructure|swa deploy/);
});
test("pipeline entrypoint scripts are present in the relocated package", async () => {
  for (const name of [
    "Initialize-NightlyMetrics.ps1", "Collect-DotNetMetrics.ps1", "Publish-Metrics.ps1",
    "Install-TestDependencies.ps1", "Validate.ps1",
  ]) assert.ok((await readFile(new URL(`../${name}`, import.meta.url))).length > 0);
});
