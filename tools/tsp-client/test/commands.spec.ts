import { cp, stat, rm, readFile, mkdir } from "node:fs/promises";
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
import { TspLocation } from "../src/typespec.js";
import { writeTspLocationYaml } from "../src/utils.js";
import { dirname } from "node:path";

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
    const emitterPackageLock = joinPaths(repoRoot, "eng", "emitter-package-lock.json");
    if (await doesFileExist(emitterPackageLock)) {
      await rm(emitterPackageLock);
    }

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

  it("Generate lock file with altername package path", async () => {
    try {
      // delete the existing lock file if it exists
      const lockFilePath = joinPaths(
        repoRoot,
        "tools/tsp-client/test/utils/alternate-emitter-package-lock.json",
      );
      if (await doesFileExist(lockFilePath)) {
        await rm(lockFilePath);
      }
      await generateLockFileCommand({
        "emitter-package-json-path": joinPaths(
          repoRoot,
          "tools/tsp-client/test/utils/alternate-emitter-package.json",
        ),
      });

      assert.isTrue((await stat(lockFilePath)).isFile());
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

  it("Update example sdk with custom emitter-package.json path", async () => {
    try {
      const tspLocationContent: TspLocation = {
        directory: "specification/contosowidgetmanager/Contoso.WidgetManager",
        commit: "45924e49834c4e01c0713e6b7ca21f94be17e396",
        repo: "Azure/azure-rest-api-specs",
        additionalDirectories: ["specification/contosowidgetmanager/Contoso.WidgetManager.Shared"],
        emitterPackageJsonPath: "tools/tsp-client/test/utils/emitter-package.json",
      };
      await writeTspLocationYaml(
        tspLocationContent,
        joinPaths(cwd(), "test/examples/sdk/alternate-emitter-package-json-path"),
      );
      const args = {
        "output-dir": joinPaths(cwd(), "test/examples/sdk/alternate-emitter-package-json-path"),
        "save-inputs": true,
      };
      await updateCommand(args);
    } catch (error) {
      assert.fail(`Failed to generate. Error: ${error}`);
    }
  });

  it("Update example sdk with custom emitter-package.json path with alternate name", async () => {
    try {
      const tspLocationContent: TspLocation = {
        directory: "specification/contosowidgetmanager/Contoso.WidgetManager",
        commit: "45924e49834c4e01c0713e6b7ca21f94be17e396",
        repo: "Azure/azure-rest-api-specs",
        additionalDirectories: ["specification/contosowidgetmanager/Contoso.WidgetManager.Shared"],
        emitterPackageJsonPath: "tools/tsp-client/test/utils/alternate-emitter-package.json",
      };
      await writeTspLocationYaml(
        tspLocationContent,
        joinPaths(cwd(), "test/examples/sdk/alternate-emitter-package-json-path"),
      );
      const args = {
        "output-dir": joinPaths(cwd(), "test/examples/sdk/alternate-emitter-package-json-path"),
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
          "https://github.com/Azure/azure-rest-api-specs/blob/45924e49834c4e01c0713e6b7ca21f94be17e396/specification/contosowidgetmanager/Contoso.WidgetManager/tspconfig.yaml",
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
        commit: "45924e49834c4e01c0713e6b7ca21f94be17e396",
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

  it("Init with --update-if-exists", async () => {
    try {
      const args = {
        "output-dir": joinPaths(cwd(), "./test/examples/initOrUpdate/"),
        "tsp-config": joinPaths(
          cwd(),
          "./test/examples/specification/contosowidgetmanager/Contoso.WidgetManager/",
        ),
        "update-if-exists": true,
        commit: "abc",
      };

      // Add a tsp-location.yaml file to the output directory for the initOrUpdate test to simulate an existing project
      const existingTspLocation: TspLocation = {
        directory: "specification/contosowidgetmanager/Contoso.WidgetManager",
        commit: "45924e49834c4e01c0713e6b7ca21f94be17e396",
        repo: "Azure/azure-rest-api-specs",
        additionalDirectories: [
          "tools/tsp-client/test/examples/specification/contosowidgetmanager/Contoso.WidgetManager.Shared",
        ],
        emitterPackageJsonPath: "tools/tsp-client/test/utils/emitter-package.json",
      };
      await mkdir(
        joinPaths(
          cwd(),
          "test/examples/initOrUpdate/sdk/contosowidgetmanager/contosowidgetmanager-rest",
        ),
        { recursive: true },
      );
      await writeTspLocationYaml(
        existingTspLocation,
        joinPaths(
          cwd(),
          "test/examples/initOrUpdate/sdk/contosowidgetmanager/contosowidgetmanager-rest",
        ),
      );
      // Now run the init command with --update-if-exists with a local spec so that we can pass in a dummy commit for testing
      const outputDir = await initCommand(args);
      const tspLocation = await readTspLocation(outputDir);
      assert.deepEqual(tspLocation, {
        directory: "specification/contosowidgetmanager/Contoso.WidgetManager",
        commit: "abc",
        repo: "Azure/azure-rest-api-specs",
        additionalDirectories: [
          "tools/tsp-client/test/examples/specification/contosowidgetmanager/Contoso.WidgetManager.Shared",
        ],
        emitterPackageJsonPath: "tools/tsp-client/test/utils/emitter-package.json",
      });
      await rm("./test/examples/initOrUpdate/", { recursive: true });
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
      assert.equal(emitterJson["dependencies"]["@azure-tools/typespec-ts"], "0.38.4");
      assert.equal(emitterJson["devDependencies"]["@typespec/compiler"], "~0.67.0");
      assert.isUndefined(emitterJson["overrides"]);
      assert.isTrue(await doesFileExist(joinPaths(repoRoot, "eng", "emitter-package-lock.json")));
    } catch (error: any) {
      assert.fail("Failed to generate tsp-client config files. Error: " + error);
    }
  }, 360000);

  it("Generate config files with alternate json path", async () => {
    try {
      // delete the existing package JSON file if it exists
      const packageJsonPath = joinPaths(
        repoRoot,
        "tools/tsp-client/test/utils/alternate-emitter-package.json",
      );
      if (await doesFileExist(packageJsonPath)) {
        await rm(packageJsonPath);
      }

      const args = {
        "package-json": joinPaths(cwd(), "test", "examples", "package.json"),
        "emitter-package-json-path": packageJsonPath,
      };
      repoRoot = await getRepoRoot(cwd());
      await generateConfigFilesCommand(args);
      assert.isTrue(await doesFileExist(packageJsonPath));
      const emitterJson = JSON.parse(await readFile(packageJsonPath, "utf8"));
      assert.equal(emitterJson["dependencies"]["@azure-tools/typespec-ts"], "0.38.4");
      assert.equal(emitterJson["devDependencies"]["@typespec/compiler"], "~0.67.0");
      assert.isUndefined(emitterJson["overrides"]);
      assert.isTrue(
        await doesFileExist(
          joinPaths(repoRoot, "tools/tsp-client/test/utils/alternate-emitter-package-lock.json"),
        ),
      );
    } catch (error: any) {
      assert.fail("Failed to generate tsp-client config files. Error: " + error);
    }
  }, 360000);

  it("Update config files with manual dependencies only package.json", async () => {
    try {
      const emitterPackageJsonPath = joinPaths(
        repoRoot,
        "tools/tsp-client/test/utils/emitter-package-extra-dep-a.json",
      );
      const args = {
        "package-json": joinPaths(cwd(), "test", "examples", "package.json"),
        "emitter-package-json-path": emitterPackageJsonPath,
      };
      repoRoot = await getRepoRoot(cwd());
      await generateConfigFilesCommand(args);
      const emitterJson = JSON.parse(await readFile(emitterPackageJsonPath, "utf8"));
      assert.equal(emitterJson["dependencies"]["@azure-tools/typespec-ts"], "0.38.4");
      assert.equal(emitterJson["devDependencies"]["@typespec/compiler"], "~0.67.0");
      //Check that the manual dependency version remains unchanged
      assert.equal(emitterJson["devDependencies"]["vitest"], "3.1.0");
      assert.isUndefined(emitterJson["overrides"]);
      assert.isTrue(
        await doesFileExist(
          joinPaths(repoRoot, "tools/tsp-client/test/utils/emitter-package-extra-dep-a-lock.json"),
        ),
      );
      // Clean up the generated files
      await rm(joinPaths(dirname(emitterPackageJsonPath), "emitter-package-extra-dep-a-lock.json"));
    } catch (error: any) {
      assert.fail("Failed to generate tsp-client config files. Error: " + error);
    }
  }, 360000);

  it("Update config files with manually added regular dependencies", async () => {
    try {
      const emitterPackageJsonPath = joinPaths(
        repoRoot,
        "tools/tsp-client/test/utils/emitter-package-manual-deps.json",
      );
      const args = {
        "package-json": joinPaths(cwd(), "test", "examples", "package.json"),
        "emitter-package-json-path": emitterPackageJsonPath,
      };
      repoRoot = await getRepoRoot(cwd());
      await generateConfigFilesCommand(args);
      const emitterJson = JSON.parse(await readFile(emitterPackageJsonPath, "utf8"));
      assert.equal(emitterJson["dependencies"]["@azure-tools/typespec-ts"], "0.38.4");
      assert.equal(emitterJson["devDependencies"]["@typespec/compiler"], "~0.67.0");
      //Check that the manual regular dependency version remains unchanged
      assert.equal(emitterJson["dependencies"]["lodash"], "4.17.21");
      //Check that the manual dev dependency version remains unchanged
      assert.equal(emitterJson["devDependencies"]["vitest"], "3.1.0");
      assert.isUndefined(emitterJson["overrides"]);
      assert.isTrue(
        await doesFileExist(
          joinPaths(repoRoot, "tools/tsp-client/test/utils/emitter-package-manual-deps-lock.json"),
        ),
      );
      // Clean up the generated files
      await rm(joinPaths(dirname(emitterPackageJsonPath), "emitter-package-manual-deps-lock.json"));
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
      assert.equal(emitterJson["dependencies"]["@azure-tools/typespec-ts"], "0.38.4");
      assert.exists(emitterJson["overrides"]);
      assert.equal(emitterJson["overrides"]["prettier"], "3.5.3");
      assert.isTrue(await doesFileExist(joinPaths(repoRoot, "eng", "emitter-package-lock.json")));
    } catch (error: any) {
      assert.fail("Failed to generate tsp-client config files. Error: " + error);
    }
  }, 360000);

  it.skip("Generate config files using azure-sdk/emitter-package-json-pinning", async () => {
    try {
      const args = {
        "package-json": joinPaths(cwd(), "test", "examples", "package-sdk-pinning.json"),
      };
      repoRoot = await getRepoRoot(cwd());
      await generateConfigFilesCommand(args);
      assert.isTrue(await doesFileExist(joinPaths(repoRoot, "eng", "emitter-package.json")));
      const emitterJson = JSON.parse(
        await readFile(joinPaths(repoRoot, "eng", "emitter-package.json"), "utf8"),
      );
      assert.equal(emitterJson["dependencies"]["@azure-tools/typespec-ts"], "0.38.4");
      assert.equal(Object.keys(emitterJson["devDependencies"]).length, 2);
      assert.equal(emitterJson["devDependencies"]["@typespec/compiler"], "~0.67.0");
      assert.isTrue(await doesFileExist(joinPaths(repoRoot, "eng", "emitter-package-lock.json")));
    } catch (error: any) {
      assert.fail("Failed to generate tsp-client config files. Error: " + error);
    }
  }, 360000);
});
