import { cp, stat, rm } from "node:fs/promises";
import {
  initCommand,
  generateCommand,
  syncCommand,
  updateCommand,
  generateLockFileCommand,
} from "../src/commands.js";
import { after, before, describe, it } from "node:test";
import { assert } from "chai";
import { getRepoRoot } from "../src/git.js";
import { cwd } from "node:process";
import { joinPaths } from "@typespec/compiler";

describe("Verify commands", async function () {
  //   before(async function () {
  //     await cp(
  //       "./test/utils/emitter-package.json",
  //       joinPaths(await getRepoRoot(cwd()), "eng", "emitter-package.json"),
  //     );
  //   });

  //   after(async function () {
  //     await rm(joinPaths(await getRepoRoot(cwd()), "eng", "emitter-package.json"));
  //     // This is generated in the first test using the command
  //     await rm(joinPaths(await getRepoRoot(cwd()), "eng", "emitter-package-lock.json"));
  //     await rm(
  //       "./test/examples/sdk/contosowidgetmanager/contosowidgetmanager-rest/TempTypeSpecFiles/",
  //       { recursive: true },
  //     );
  //     await rm("./test/examples/sdk/local-spec-sdk/TempTypeSpecFiles/", { recursive: true });
  //   });

  await it("Generate lock file", async function () {
    try {
      await generateLockFileCommand({});

      const repoRoot = await getRepoRoot(cwd());
      assert.isTrue((await stat(joinPaths(repoRoot, "eng", "emitter-package-lock.json"))).isFile());
    } catch (error) {
      assert.fail(`Failed to generate lock file. Error: ${error}`);
    }
  });

  await it("Sync example sdk", async function () {
    try {
      const args = {
        "output-dir": joinPaths(
          cwd(),
          "./test/examples/sdk/contosowidgetmanager/contosowidgetmanager-rest",
        ),
      };
      await syncCommand(args);
    } catch (error) {
      assert.fail(`Failed to sync files. Error: ${error}`);
    }
    const dir = await stat(
      "./test/examples/sdk/contosowidgetmanager/contosowidgetmanager-rest/TempTypeSpecFiles/",
    );
    assert.isTrue(dir.isDirectory());
  });

  await it("Sync example sdk with local spec", async function () {
    try {
      const args = {
        "output-dir": joinPaths(cwd(), "./test/examples/sdk/local-spec-sdk"),
        "local-spec-repo":
          "./test/examples/specification/contosowidgetmanager/Contoso.WidgetManager",
      };
      await syncCommand(args);
    } catch (error) {
      assert.fail(`Failed to sync files. Error: ${error}`);
    }
    const dir = await stat("./test/examples/sdk/local-spec-sdk/TempTypeSpecFiles/");
    assert.isTrue(dir.isDirectory());
  });

  await it("Generate example sdk", async function () {
    try {
      const args = {
        "output-dir": joinPaths(
          cwd(),
          "./test/examples/sdk/contosowidgetmanager/contosowidgetmanager-rest",
        ),
        "save-inputs": true,
      };
      await generateCommand(args);
    } catch (error) {
      assert.fail(`Failed to generate. Error: ${error}`);
    }
    const dir = await stat(
      "./test/examples/sdk/contosowidgetmanager/contosowidgetmanager-rest/tsp-location.yaml",
    );
    assert.isTrue(dir.isFile());
  });

  await it("Update example sdk", async function () {
    try {
      const args = {
        "output-dir": joinPaths(
          cwd(),
          "./test/examples/sdk/contosowidgetmanager/contosowidgetmanager-rest",
        ),
        "save-inputs": true,
      };
      await updateCommand(args);
    } catch (error) {
      assert.fail(`Failed to generate. Error: ${error}`);
    }
  });

  await it("Update example sdk & pass tspconfig.yaml", async function () {
    try {
      const args = {
        "output-dir": joinPaths(
          cwd(),
          "./test/examples/sdk/contosowidgetmanager/contosowidgetmanager-rest",
        ),
        "tsp-config":
          "https://github.com/Azure/azure-rest-api-specs/blob/db63bea839f5648462c94e685d5cc96f8e8b38ba/specification/contosowidgetmanager/Contoso.WidgetManager/tspconfig.yaml",
        "save-inputs": true,
      };
      await updateCommand(args);
    } catch (error) {
      assert.fail(`Failed to generate. Error: ${error}`);
    }
  });

  await it("Update example sdk & pass commit", async function () {
    try {
      const args = {
        "output-dir": joinPaths(
          cwd(),
          "./test/examples/sdk/contosowidgetmanager/contosowidgetmanager-rest",
        ),
        commit: "db63bea839f5648462c94e685d5cc96f8e8b38ba",
        "save-inputs": true,
      };
      await updateCommand(args);
    } catch (error) {
      assert.fail(`Failed to update. Error: ${error}`);
    }
  });

  await it("Update example sdk & pass only --repo", async function () {
    try {
      const args = {
        "output-dir": joinPaths(
          cwd(),
          "./test/examples/sdk/contosowidgetmanager/contosowidgetmanager-rest",
        ),
        repo: "foo",
        "save-inputs": true,
      };
      await updateCommand(args);
      assert.fail("Should have failed");
    } catch (error: any) {
      assert.equal(
        error.message,
        "Commit SHA is required when specifying `--repo`; please specify a commit using `--commit`",
      );
    }
  });

  await it.skip("Init example sdk", async function () {
    try {
      const args = {
        "output-dir": joinPaths(cwd(), "./test/examples/"),
        "tsp-config":
          "https://github.com/Azure/azure-rest-api-specs/blob/7ed015e3dd1b8b1b0e71c9b5e6b6c5ccb8968b3a/specification/cognitiveservices/ContentSafety/tspconfig.yaml",
      };
      await initCommand(args);

      assert.isTrue(
        (
          await stat("./test/examples/sdk/contentsafety/ai-content-safety-rest/package.json")
        ).isFile(),
      );

      // Clean up directory for other init tests
      // await removeDirectory("./test/examples/sdk/contentsafety/ai-content-safety-rest");
    } catch (error: any) {
      assert.fail("Failed to init. Error: " + error);
    }
  });

  await it.skip("Init with --skip-sync-and-generate", async function () {
    try {
      const args = {
        "output-dir": joinPaths(cwd(), "./test/examples/"),
        "tsp-config":
          "https://github.com/Azure/azure-rest-api-specs/blob/7ed015e3dd1b8b1b0e71c9b5e6b6c5ccb8968b3a/specification/cognitiveservices/ContentSafety/tspconfig.yaml",
        "skip-sync-and-generate": true,
      };
      await initCommand(args);

      const tspLocation = await stat(
        "./test/examples/sdk/contentsafety/ai-content-safety-rest/tsp-location.yaml",
      );
      assert.isTrue(tspLocation.isFile());
    } catch (error: any) {
      assert.fail("Failed to init. Error: " + error);
    }
  });
});
