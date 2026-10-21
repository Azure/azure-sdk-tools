// Verifies configured eval definitions load without executing models, MCP servers or fixtures.
import assert from "node:assert/strict";
import { resolve } from "node:path";
import { test } from "node:test";
import { discoverAndLoadEvals, ProjectContext } from "@microsoft/vally";

const repoRoot = resolve(import.meta.dirname, "../../..");

// Use the existing declared projects; the separately owned TypeSpec benchmark is not added here.
for (const [name, directory] of [
    ["workflow/tool", "evals"],
    ["skill", ".github/skills"],
]) {
    test(`configured ${name} evals load without omitted schemas`, async () => {
        const projectRoot = resolve(repoRoot, directory);
        const project = await ProjectContext.load(projectRoot);
        assert.equal(resolve(project.dir), projectRoot, "The expected .vally.yaml must be loaded");

        const { evals, errors, scannedCount } = await discoverAndLoadEvals(project, {
            suiteParams: project.config.params,
        });

        assert.deepEqual(errors, [], errors.map(({ path, reason }) => `${path}: ${reason}`).join("\n"));
        assert.ok(scannedCount > 0, "The configured project must discover eval definitions");
        assert.equal(evals.length, scannedCount, "No discovered eval may be silently omitted");
    });
}
