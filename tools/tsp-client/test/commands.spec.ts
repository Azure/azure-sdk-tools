import { cp, stat, rm, readFile } from "node:fs/promises";
import {
  initCommand,
  generateCommand,
  syncCommand,
  updateCommand,
  generateLockFileCommand,
  generateConfigFilesCommand,
} from "../src/commands.js";
import { afterAll, beforeAll, describe, it } from "vitest";
import { assert } from "chai";
import { getRepoRoot } from "../src/git.js";
import { cwd } from "node:process";
import { joinPaths } from "@typespec/compiler";
import { readTspLocation, removeDirectory } from "../src/fs.js";
import { doesFileExist } from "../src/network.js";

describe.sequential("Verify commands", () => {
  let repoRoot;
  beforeAll(async () => {
    repoRoot = await getRepoRoot(cwd());
    await cp(
      "./test/utils/emitter-package.json",
      joinPaths(repoRoot, "eng", "emitter-package.json"),
    );
  });

  afterAll(async () => {
    await rm(joinPaths(repoRoot, "eng", "emitter-package.json"));
    // This is generated in the first test using the command
    await rm(joinPaths(repoRoot, "eng", "emitter-package-lock.json"));
    await rm(
      "./test/examples/sdk/contosowidgetmanager/contosowidgetmanager-rest/TempTypeSpecFiles/",
      { recursive: true },
    );
    await rm("./test/examples/sdk/local-spec-sdk/TempTypeSpecFiles/", { recursive: true });
  });

  it("Generate lock file", async () => {
    try {
      await generateLockFileCommand({});

      assert.isTrue((await stat(joinPaths(repoRoot, "eng", "emitter-package-lock.json"))).isFile());
    } catch (error) {
      assert.fail(`Failed to generate lock file. Error: ${error}`);
    }
  });

  it("Sync example sdk", async () => {
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

  it("Sync example sdk with local spec", async () => {
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

  it("Generate example sdk", async () => {
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

  it("Generate with alternate entrypoint", async () => {
    try {
      const args = {
        "output-dir": joinPaths(cwd(), "./test/examples/sdk/alternate-entrypoint"),
        "local-spec-repo":
          "./test/examples/specification/contosowidgetmanager/Contoso.WidgetManager",
      };
      await updateCommand(args);
    } catch (error) {
      assert.fail(`Failed to generate. Error: ${error}`);
    }
    const tspLocation = await readTspLocation("./test/examples/sdk/alternate-entrypoint");
    assert.equal(tspLocation.entrypointFile, "foo.tsp");
  });

  it("Update example sdk", async () => {
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

  it("Update example sdk & pass tspconfig.yaml", async () => {
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

  it("Update example sdk & pass commit", async () => {
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

  it("Update example sdk & pass only --repo", async () => {
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

  it.skip("Init example sdk", async () => {
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

  it.skip("Init with --skip-sync-and-generate", async () => {
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

  it("Init with local spec", async () => {
    try {
      const args = {
        "output-dir": joinPaths(cwd(), "./test/examples/init/"),
        "tsp-config": joinPaths(
          cwd(),
          "./test/examples/specification/contosowidgetmanager/Contoso.WidgetManager/",
        ),
      };
      const outputDir = await initCommand(args);
      const tspLocation = await readTspLocation(outputDir);
      assert.equal(tspLocation.commit, "<replace with your value>");
      assert.equal(tspLocation.repo, "<replace with your value>");
      await removeDirectory(joinPaths(cwd(), "./test/examples/init/sdk"));
    } catch (error: any) {
      assert.fail("Failed to init. Error: " + error);
    }
  });

  it("Generate config files", async () => {
    try {
      const args = {
        "package-json": joinPaths(cwd(), "test", "examples", "package.json"),
      };
      repoRoot = await getRepoRoot(cwd());
      await generateConfigFilesCommand(args);
      assert.isTrue(await doesFileExist(joinPaths(repoRoot, "eng", "emitter-package.json")));
      const emitterJson = JSON.parse(
        await readFile(joinPaths(repoRoot, "eng", "emitter-package.json"), "utf8"),
      );
      assert.equal(emitterJson["dependencies"]["@azure-tools/typespec-python"], "0.37.3");
      assert.equal(emitterJson["devDependencies"]["@typespec/compiler"], "~0.63.0");
      assert.isUndefined(emitterJson["overrides"]);
      assert.isTrue(await doesFileExist(joinPaths(repoRoot, "eng", "emitter-package-lock.json")));
    } catch (error: any) {
      assert.fail("Failed to generate tsp-client config files. Error: " + error);
    }
  });

  it("Generate config files with overrides", async () => {
    try {
      const args = {
        "package-json": joinPaths(cwd(), "test", "examples", "package.json"),
        overrides: joinPaths(cwd(), "test", "examples", "overrides.json"),
      };
      repoRoot = await getRepoRoot(cwd());
      await generateConfigFilesCommand(args);
      assert.isTrue(await doesFileExist(joinPaths(repoRoot, "eng", "emitter-package.json")));
      const emitterJson = JSON.parse(
        await readFile(joinPaths(repoRoot, "eng", "emitter-package.json"), "utf8"),
      );
      assert.equal(emitterJson["dependencies"]["@azure-tools/typespec-python"], "0.36.0");
      assert.exists(emitterJson["overrides"]);
      assert.equal(emitterJson["overrides"]["@typespec/compiler"], "0.61.0");
      assert.isTrue(await doesFileExist(joinPaths(repoRoot, "eng", "emitter-package-lock.json")));
    } catch (error: any) {
      assert.fail("Failed to generate tsp-client config files. Error: " + error);
    }
  });
});
