#!/usr/bin/env node

const { execFileSync } = require("node:child_process");
const { parseArgs } = require("./lib/v2-common");

const args = parseArgs(process.argv.slice(2), {
  days: 180,
  startAt: "2026-03-01T00:00:00.000Z",
  limit: 10,
  mode: "complete",
  buildId: new Date().toISOString().replace(/[-:]/g, "").slice(0, 13),
});

run("scripts/collect-release-plans.js", [
  "--days",
  args.days,
  "--start-at",
  args.startAt,
  "--limit",
  args.limit,
  "--mode",
  args.mode,
]);
run("scripts/collect-github-prs.js", []);
run("scripts/collect-github-releases.js", []);
run("scripts/collect-pipeline-runs.js", []);
run("scripts/build-view-data.js", ["--build-id", args.buildId]);
run("scripts/validate-data.js", []);

function run(script, values) {
  execFileSync(process.execPath, [script, ...values.map(String)], {
    stdio: "inherit",
  });
}
