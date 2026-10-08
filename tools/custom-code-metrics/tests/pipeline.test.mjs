import { test } from "node:test";
import assert from "node:assert/strict";
import { readFile, access } from "node:fs/promises";

const pipeline = await readFile(new URL("../ci.yml", import.meta.url), "utf8");

test("shared CI validates tools without scheduling or checking out language repositories", () => {
  assert.match(pipeline, /tools\/custom-code-metrics/);
  assert.match(pipeline, /1es-redirect\.yml/);
  assert.match(pipeline, /Validate\.ps1/);
  assert.doesNotMatch(pipeline, /schedules:|azure-sdk-for-net|AzureCLI@|Publish-Metrics/);
});
test("only one public measurement command remains, without central nightly orchestration", async () => {
  assert.ok((await readFile(new URL("../Collect-Metrics.ps1", import.meta.url))).length > 0);
  for (const file of ["Collect-DotNetMetrics.ps1", "Initialize-NightlyMetrics.ps1", "nightly.yml"]) {
    await assert.rejects(access(new URL(`../${file}`, import.meta.url)), { code: "ENOENT" });
  }
});
test("language-owned caller measures first and publishes the actual returned observation", async () => {
  const readme = await readFile(new URL("../README.md", import.meta.url), "utf8");
  assert.match(readme, /\$snapshot = & .*Collect-Metrics\.ps1/);
  assert.match(readme, /-Language dotnet -RepoRoot \$repo -OutputDirectory/);
  assert.match(readme, /Publish-Metrics\.ps1 -SnapshotPath \$snapshot/);
  assert.match(readme, /Language repositories own their schedules, trusted checkouts and publication jobs/);
  assert.doesNotMatch(readme, /nightly\.yml|Collect-DotNetMetrics|Initialize-NightlyMetrics/);
});
test("measurement is separate from publication and dispatch fails explicitly for unimplemented languages", async () => {
  const command = await readFile(new URL("../Collect-Metrics.ps1", import.meta.url), "utf8");
  assert.match(command, /ValidateSet\("dotnet", "java", "js", "python", "go", "rust", "cpp"\)/);
  assert.match(command, /not implemented/);
  assert.match(command, /check-copy/);
  assert.match(command, /Invoke-Pester/);
  assert.doesNotMatch(command, /git clone|git fetch|AzureCLI|& az|publishing\.mjs|Write-Host.*task\.setvariable/);
});
