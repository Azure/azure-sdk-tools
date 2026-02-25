import { cp, stat, rm, readFile, mkdir } from "node:fs/promises";
import {
  initCommand,
  generateCommand,
  syncCommand,
  updateCommand,
  generateLockFileCommand,
  generateConfigFilesCommand,
} from "../src/commands.js";
import { afterAll, afterEach, beforeAll, describe, it, expect } from "vitest";
import { assert } from "chai";
import { getRepoRoot } from "../src/git.js";
import { cwd } from "node:process";
import { joinPaths } from "@typespec/compiler";
import { readTspLocation, removeDirectory } from "../src/fs.js";
import { doesFileExist } from "../src/network.js";
import { TspLocation } from "../src/typespec.js";
import { writeTspLocationYaml } from "../src/utils.js";
import { dirname, resolve } from "node:path";

describe.sequential("Verify commands", () => {
  let repoRoot: string;
  beforeAll(async () => {
    repoRoot = await getRepoRoot(cwd());
    await cp(
      "./test/utils/emitter-package.json",
      joinPaths(repoRoot, "eng", "emitter-package.json"),
    );
    await mkdir(joinPaths(cwd(), "test/examples/initGlobalConfig/"), { recursive: true });
    await mkdir(joinPaths(cwd(), "test/examples/initGlobalConfigNoMatch/"), { recursive: true });
  });

  afterEach(async () => {
    await rm(resolve(joinPaths(repoRoot, "/sdk/contosowidgetmanager")), {
      recursive: true,
      force: true,
    });
  });

  afterAll(async () => {
    // Clean up tspclientconfig.yaml if it exists
    const tspclientconfig = joinPaths(repoRoot, "eng", "tspclientconfig.yaml");
    if (await doesFileExist(tspclientconfig)) {
      await rm(tspclientconfig);
    }

    // Clean up alternate-emitter-package-json-path directory
    const alternateTspLocation = joinPaths(
      cwd(),
      "test/examples/sdk/alternate-emitter-package-json-path/tsp-location.yaml",
    );
    if (await doesFileExist(alternateTspLocation)) {
      await rm(alternateTspLocation);
    }

    await rm(joinPaths(repoRoot, "eng", "emitter-package.json"), { force: true });
    await rm(joinPaths(repoRoot, "eng", "emitter-package-lock.json"), { force: true });
    await rm("./test/examples/sdk/local-spec-sdk/TempTypeSpecFiles/", {
      recursive: true,
      force: true,
    });
    await rm("./test/examples/initGlobalConfig/", { recursive: true, force: true });
    await rm("./test/examples/initGlobalConfigNoMatch/", { recursive: true, force: true });
    await rm(
      "./test/examples/sdk/contosowidgetmanager/contosowidgetmanager-rest/TempTypeSpecFiles/",
      { recursive: true, force: true },
    );
    await rm(joinPaths(repoRoot, "sdk/keyvault"), { recursive: true, force: true });
  });

  it("Generate lock file", async () => {
    try {
      await generateLockFileCommand({});

      assert.isTrue((await stat(joinPaths(repoRoot, "eng", "emitter-package-lock.json"))).isFile());
    } catch (error) {
      assert.fail(`Failed to generate lock file. Error: ${error}`);
    }
  });

  it("Generate lock file with alternate package path", async () => {
    const tmpDir = joinPaths(cwd(), ".tmp-test-lock-file-alt");
    try {
      // Create temporary directory and copy the package.json
      await mkdir(tmpDir, { recursive: true });
      const tmpPackageJsonPath = joinPaths(tmpDir, "alternate-emitter-package.json");
      await cp(
        joinPaths(repoRoot, "tools/tsp-client/test/utils/alternate-emitter-package.json"),
        tmpPackageJsonPath,
      );

      await generateLockFileCommand({
        "emitter-package-json-path": tmpPackageJsonPath,
      });

      const lockFilePath = joinPaths(tmpDir, "alternate-emitter-package-lock.json");
      assert.isTrue((await stat(lockFilePath)).isFile());
    } catch (error) {
      assert.fail(`Failed to generate lock file. Error: ${error}`);
    } finally {
      // Clean up temporary directory
      if (await doesFileExist(tmpDir)) {
        await rm(tmpDir, { recursive: true });
      }
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

  it("Sync and generate example sdk with local spec", async () => {
    const args = {
      "output-dir": joinPaths(cwd(), "./test/examples/sdk/local-spec-sdk"),
      "local-spec-repo": "./test/examples/specification/contosowidgetmanager/Contoso.WidgetManager",
      "save-inputs": true,
    };
    try {
      await syncCommand(args);
    } catch (error) {
      assert.fail(`Failed to sync files. Error: ${error}`);
    }
    assert.isTrue(
      (await stat("./test/examples/sdk/local-spec-sdk/TempTypeSpecFiles/")).isDirectory(),
    );
    try {
      await generateCommand(args);
    } catch (error) {
      assert.fail(`Failed to generate. Error: ${error}`);
    }
    assert.isTrue(
      (
        await stat(joinPaths(repoRoot, "sdk/contosowidgetmanager/contosowidgetmanager-rest/"))
      ).isDirectory(),
    );
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
      assert.isTrue(
        (
          await stat(joinPaths(repoRoot, "sdk/contosowidgetmanager/contoso-widgetmanager/"))
        ).isDirectory(),
      );

      // Explicitly assert that we're not appending the current directory to the output path which would happen
      // if we pass in the current directory to replace output-dir in the following format:
      // emitter-output-dir: "{output-dir}/{service-dir}/contosowidgetmanager-rest"
      try {
        await stat(
          "./test/examples/sdk/contosowidgetmanager/contosowidgetmanager-rest/sdk/contosowidgetmanager/",
        );
      } catch (error) {
        assert.equal(error.code, "ENOENT");
      }
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
        additionalDirectories: [
          "tools/tsp-client/test/examples/specification/contosowidgetmanager/Contoso.WidgetManager.Shared",
        ],
        emitterPackageJsonPath: "tools/tsp-client/test/utils/emitter-package.json",
      };
      await writeTspLocationYaml(
        tspLocationContent,
        joinPaths(cwd(), "test/examples/sdk/alternate-emitter-package-json-path"),
      );
      const args = {
        "output-dir": joinPaths(cwd(), "test/examples/sdk/alternate-emitter-package-json-path"),
        "local-spec-repo":
          "./test/examples/specification/contosowidgetmanager/Contoso.WidgetManager",
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
        additionalDirectories: [
          "tools/tsp-client/test/examples/specification/contosowidgetmanager/Contoso.WidgetManager.Shared",
        ],
        emitterPackageJsonPath: "tools/tsp-client/test/utils/alternate-emitter-package.json",
      };
      await mkdir(joinPaths(repoRoot, "sdk/contosowidgetmanager/contosowidgetmanager-rest"), {
        recursive: true,
      });
      await writeTspLocationYaml(
        tspLocationContent,
        joinPaths(repoRoot, "sdk/contosowidgetmanager/contosowidgetmanager-rest"),
      );
      const args = {
        "output-dir": joinPaths(repoRoot, "sdk/contosowidgetmanager/contosowidgetmanager-rest"),
        "local-spec-repo":
          "./test/examples/specification/contosowidgetmanager/Contoso.WidgetManager",
      };
      await updateCommand(args);
    } catch (error) {
      assert.fail(`Failed to generate. Error: ${error}`);
    }
  });

  // TODO: unskip after updates have been merged to upstream tspconfig.yaml files
  it.skip("Update example sdk & pass tspconfig.yaml", async () => {
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

  // TODO: unskip after updates have been merged to upstream tspconfig.yaml files
  it.skip("Update example sdk & pass commit", async () => {
    try {
      const args = {
        "output-dir": joinPaths(
          cwd(),
          "./test/examples/sdk/contosowidgetmanager/contosowidgetmanager-rest",
        ),
        commit: "45924e49834c4e01c0713e6b7ca21f94be17e396",
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

  it.skip("Init with legacy package path resolution", async () => {
    try {
      const args = {
        "output-dir": joinPaths(cwd(), "./test/examples/init/"),
        "tsp-config": joinPaths(
          cwd(),
          "./test/examples/specification/contosowidgetmanager-legacy-package-dir/Contoso.WidgetManager/",
        ),
      };
      const outputDir = await initCommand(args);
      assert.equal(
        outputDir,
        resolve(
          joinPaths(cwd(), "./test/examples/init/", "sdk/legacypath", "contosowidgetmanager-rest"),
        ),
      );
      // Make sure that the emitter-output-dir value is ignored
      try {
        await stat(
          joinPaths(
            cwd(),
            "./test/examples/specification/contosowidgetmanager-legacy-package-dir/nonexistent",
          ),
        );
        assert.fail("The emitter-output-dir path should not exist");
      } catch (error: any) {
        assert.match(error.message, /ENOENT: no such file or directory/);
      }
      await removeDirectory(joinPaths(cwd(), "./test/examples/init/sdk"));
    } catch (error: any) {
      assert.fail("Failed to init. Error: " + error);
    }
  });

  it.skip("Init with --update-if-exists", async () => {
    try {
      const libraryPath = joinPaths(repoRoot, "sdk/contosowidgetmanager/contosowidgetmanager-rest");
      const args = {
        "output-dir": libraryPath,
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
      await mkdir(libraryPath, { recursive: true });
      await writeTspLocationYaml(existingTspLocation, libraryPath);
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
      await rm(joinPaths(repoRoot, "sdk/contosowidgetmanager"), { recursive: true });
    } catch (error: any) {
      assert.fail("Failed to init. Error: " + error);
    }
  });

  it("Init with --update-if-exists and --emitter-package-json-path", async () => {
    try {
      const libraryPath = joinPaths(repoRoot, "sdk/contosowidgetmanager/contosowidgetmanager-rest");
      const args = {
        "output-dir": libraryPath,
        "tsp-config": joinPaths(
          cwd(),
          "./test/examples/specification/contosowidgetmanager/Contoso.WidgetManager/",
        ),
        "update-if-exists": true,
        commit: "abc",
        "emitter-package-json-path": "test/utils/alternate-emitter-package.json",
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
      await mkdir(libraryPath, {
        recursive: true,
      });
      await writeTspLocationYaml(existingTspLocation, libraryPath);
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
        // The emitterPackageJsonPath should be updated to the alternate emitter package path passed through the
        // emitter-package-json-path argument
        emitterPackageJsonPath: "tools/tsp-client/test/utils/alternate-emitter-package.json",
      });
      await rm(joinPaths(repoRoot, "sdk"), { recursive: true });
    } catch (error: any) {
      assert.fail("Failed to init. Error: " + error);
    }
  });

  it.skip("Init with --update-if-exists with undefined emitterPackageJsonPath in tsp-location.yaml", async () => {
    try {
      const libraryPath = joinPaths(repoRoot, "sdk/contosowidgetmanager/contosowidgetmanager-rest");
      const args = {
        "output-dir": libraryPath,
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
      };
      await mkdir(libraryPath, {
        recursive: true,
      });
      await writeTspLocationYaml(existingTspLocation, libraryPath);
      // Now run the init command with --update-if-exists with a local spec so that we can pass in a dummy commit for testing
      const outputDir = await initCommand(args);
      const tspLocation = await readTspLocation(outputDir);
      // Verify that the tspLocation has been updated with the new commit and that emitterPackageJsonPath is still undefined
      assert.deepEqual(tspLocation, {
        directory: "specification/contosowidgetmanager/Contoso.WidgetManager",
        commit: "abc",
        repo: "Azure/azure-rest-api-specs",
        additionalDirectories: [
          "tools/tsp-client/test/examples/specification/contosowidgetmanager/Contoso.WidgetManager.Shared",
        ],
      });
      await rm(joinPaths(repoRoot, "sdk"), { recursive: true });
    } catch (error: any) {
      assert.fail("Failed to init. Error: " + error);
    }
  });

  it("Init with global tsp-client-config.yaml", async () => {
    await cp(
      joinPaths(cwd(), "test/utils/tsp-client-config.yaml"),
      joinPaths(await getRepoRoot("."), "eng", "tsp-client-config.yaml"),
    );
    try {
      const args = {
        "output-dir": joinPaths(cwd(), "./test/examples/initGlobalConfig/"),
        "tsp-config": joinPaths(
          cwd(),
          "./test/examples/specification/contosowidgetmanager/Contoso.WidgetManager/",
        ),
      };
      const outputDir = await initCommand(args);
      const tspLocation = await readTspLocation(outputDir);
      assert.deepEqual(tspLocation, {
        directory: "specification/contosowidgetmanager/Contoso.WidgetManager",
        commit: "<replace with your value>",
        repo: "<replace with your value>",
        additionalDirectories: [
          "tools/tsp-client/test/examples/specification/contosowidgetmanager/Contoso.WidgetManager.Shared",
        ],
        emitterPackageJsonPath: "tools/tsp-client/test/utils/alternate-emitter-package.json",
      });
      await rm(joinPaths(await getRepoRoot("."), "eng", "tsp-client-config.yaml"));
    } catch (error: any) {
      assert.fail("Failed to init. Error: " + error);
    }
  });

  it("Init with global tsp-client-config.yaml with no emitter matches", async () => {
    await cp(
      joinPaths(cwd(), "test/utils/tspclientconfig-no-match.yaml"),
      joinPaths(await getRepoRoot("."), "eng", "tsp-client-config.yaml"),
    );
    try {
      const args = {
        "output-dir": joinPaths(cwd(), "./test/examples/initGlobalConfigNoMatch/"),
        "tsp-config": joinPaths(
          cwd(),
          "./test/examples/specification/contosowidgetmanager/Contoso.WidgetManager/",
        ),
      };
      const outputDir = await initCommand(args);
      const tspLocation = await readTspLocation(outputDir);
      // When no emitters match between global config and tspconfig, it should fall back to default emitter-package.json
      // and emitterPackageJsonPath should be undefined (since it's using the default path)
      assert.deepEqual(tspLocation, {
        directory: "specification/contosowidgetmanager/Contoso.WidgetManager",
        commit: "<replace with your value>",
        repo: "<replace with your value>",
        additionalDirectories: [
          "tools/tsp-client/test/examples/specification/contosowidgetmanager/Contoso.WidgetManager.Shared",
        ],
      });
      await rm(joinPaths(await getRepoRoot("."), "eng", "tsp-client-config.yaml"));
    } catch (error: any) {
      assert.fail("Failed to init. Error: " + error);
    }
  });

  it("Generate config files", async () => {
    try {
      const args = {
        "package-json": joinPaths(cwd(), "test", "examples", "package.json"),
      };
      await generateConfigFilesCommand(args);
      assert.isTrue(await doesFileExist(joinPaths(repoRoot, "eng", "emitter-package.json")));
      const emitterJson = JSON.parse(
        await readFile(joinPaths(repoRoot, "eng", "emitter-package.json"), "utf8"),
      );
      assert.equal(emitterJson["dependencies"]["@azure-tools/typespec-ts"], "0.46.1");
      assert.equal(emitterJson["devDependencies"]["@typespec/compiler"], "1.6.0");
      assert.isUndefined(emitterJson["overrides"]);
      assert.isTrue(await doesFileExist(joinPaths(repoRoot, "eng", "emitter-package-lock.json")));
    } catch (error: any) {
      assert.fail("Failed to generate tsp-client config files. Error: " + error);
    }
  }, 360000);

  it("Generate config files with alternate json path", async () => {
    const tmpDir = joinPaths(cwd(), ".tmp-test-config-files-alt");
    try {
      await mkdir(tmpDir, { recursive: true });
      const packageJsonPath = joinPaths(tmpDir, "alternate-emitter-package.json");

      const args = {
        "package-json": joinPaths(cwd(), "test", "examples", "package.json"),
        "emitter-package-json-path": packageJsonPath,
      };
      await generateConfigFilesCommand(args);
      assert.isTrue(await doesFileExist(packageJsonPath));
      const emitterJson = JSON.parse(await readFile(packageJsonPath, "utf8"));
      assert.equal(emitterJson["dependencies"]["@azure-tools/typespec-ts"], "0.46.1");
      assert.equal(emitterJson["devDependencies"]["@typespec/compiler"], "1.6.0");
      assert.isUndefined(emitterJson["overrides"]);
      assert.isTrue(await doesFileExist(joinPaths(tmpDir, "alternate-emitter-package-lock.json")));
    } catch (error: any) {
      assert.fail("Failed to generate tsp-client config files. Error: " + error);
    } finally {
      // Clean up temporary directory
      if (await doesFileExist(tmpDir)) {
        await rm(tmpDir, { recursive: true });
      }
    }
  }, 360000);

  it("Update config files with manual dependencies only package.json", async () => {
    const tmpDir = joinPaths(cwd(), ".tmp-test-manual-deps-a");
    try {
      await mkdir(tmpDir, { recursive: true });
      const emitterPackageJsonPath = joinPaths(tmpDir, "emitter-package-extra-dep-a.json");
      // Copy the original file to temp directory
      await cp(
        joinPaths(repoRoot, "tools/tsp-client/test/utils/emitter-package-extra-dep-a.json"),
        emitterPackageJsonPath,
      );

      const args = {
        "package-json": joinPaths(cwd(), "test", "examples", "package.json"),
        "emitter-package-json-path": emitterPackageJsonPath,
      };
      await generateConfigFilesCommand(args);
      const emitterJson = JSON.parse(await readFile(emitterPackageJsonPath, "utf8"));
      assert.equal(emitterJson["dependencies"]["@azure-tools/typespec-ts"], "0.46.1");
      assert.equal(emitterJson["devDependencies"]["@typespec/compiler"], "1.6.0");
      //Check that the manual dependency version remains unchanged
      assert.equal(emitterJson["devDependencies"]["vitest"], "3.1.0");
      assert.isUndefined(emitterJson["overrides"]);
      assert.isTrue(
        await doesFileExist(joinPaths(tmpDir, "emitter-package-extra-dep-a-lock.json")),
      );
    } catch (error: any) {
      assert.fail("Failed to generate tsp-client config files. Error: " + error);
    } finally {
      // Clean up temporary directory
      if (await doesFileExist(tmpDir)) {
        await rm(tmpDir, { recursive: true });
      }
    }
  }, 360000);

  it("Update config files with manually added regular dependencies", async () => {
    const tmpDir = joinPaths(cwd(), ".tmp-test-manual-deps-b");
    try {
      await mkdir(tmpDir, { recursive: true });
      const emitterPackageJsonPath = joinPaths(tmpDir, "emitter-package-manual-deps.json");
      // Copy the original file to temp directory
      await cp(
        joinPaths(repoRoot, "tools/tsp-client/test/utils/emitter-package-manual-deps.json"),
        emitterPackageJsonPath,
      );

      const args = {
        "package-json": joinPaths(cwd(), "test", "examples", "package.json"),
        "emitter-package-json-path": emitterPackageJsonPath,
      };
      await generateConfigFilesCommand(args);
      const emitterJson = JSON.parse(await readFile(emitterPackageJsonPath, "utf8"));
      assert.equal(emitterJson["dependencies"]["@azure-tools/typespec-ts"], "0.46.1");
      assert.equal(emitterJson["devDependencies"]["@typespec/compiler"], "1.6.0");
      //Check that the manual regular dependency version remains unchanged
      assert.equal(emitterJson["dependencies"]["lodash"], "4.17.21");
      //Check that the manual dev dependency version remains unchanged
      assert.equal(emitterJson["devDependencies"]["vitest"], "3.1.0");
      assert.isUndefined(emitterJson["overrides"]);
      assert.isTrue(
        await doesFileExist(joinPaths(tmpDir, "emitter-package-manual-deps-lock.json")),
      );
    } catch (error: any) {
      assert.fail("Failed to generate tsp-client config files. Error: " + error);
    } finally {
      // Clean up temporary directory
      if (await doesFileExist(tmpDir)) {
        await rm(tmpDir, { recursive: true });
      }
    }
  });

  it("Generate config files with overrides", async () => {
    try {
      const args = {
        "package-json": joinPaths(cwd(), "test", "examples", "package.json"),
        overrides: joinPaths(cwd(), "test", "examples", "overrides.json"),
      };
      await generateConfigFilesCommand(args);
      assert.isTrue(await doesFileExist(joinPaths(repoRoot, "eng", "emitter-package.json")));
      const emitterJson = JSON.parse(
        await readFile(joinPaths(repoRoot, "eng", "emitter-package.json"), "utf8"),
      );
      assert.equal(emitterJson["dependencies"]["@azure-tools/typespec-ts"], "0.46.1");
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
      await generateConfigFilesCommand(args);
      assert.isTrue(await doesFileExist(joinPaths(repoRoot, "eng", "emitter-package.json")));
      const emitterJson = JSON.parse(
        await readFile(joinPaths(repoRoot, "eng", "emitter-package.json"), "utf8"),
      );
      assert.equal(emitterJson["dependencies"]["@azure-tools/typespec-ts"], "0.46.1");
      assert.equal(Object.keys(emitterJson["devDependencies"]).length, 2);
      assert.equal(emitterJson["devDependencies"]["@typespec/compiler"], "1.6.0");
      assert.isTrue(await doesFileExist(joinPaths(repoRoot, "eng", "emitter-package-lock.json")));
    } catch (error: any) {
      assert.fail("Failed to generate tsp-client config files. Error: " + error);
    }
  }, 360000);

  it("Generate config files preserves all existing fields", async () => {
    try {
      // Create a temp directory and copy the test file
      const tempDir = joinPaths(repoRoot, "tools/tsp-client/test/utils/temp-preserve-fields");
      await mkdir(tempDir, { recursive: true });
      const emitterPackageJsonPath = joinPaths(tempDir, "emitter-package-with-extra-fields.json");
      await cp(
        joinPaths(repoRoot, "tools/tsp-client/test/utils/emitter-package-with-extra-fields.json"),
        emitterPackageJsonPath,
      );

      const args = {
        "package-json": joinPaths(cwd(), "test", "examples", "package.json"),
        "emitter-package-json-path": emitterPackageJsonPath,
      };
      await generateConfigFilesCommand(args);
      const emitterJson = JSON.parse(await readFile(emitterPackageJsonPath, "utf8"));

      // Check that dependencies and devDependencies are updated
      assert.equal(emitterJson["dependencies"]["@azure-tools/typespec-ts"], "0.46.1");
      assert.equal(emitterJson["devDependencies"]["@typespec/compiler"], "1.6.0");

      // Check that all other fields are preserved
      assert.equal(emitterJson["name"], "test-emitter");
      assert.equal(emitterJson["version"], "1.0.0");
      assert.equal(emitterJson["description"], "Test emitter package with extra fields");
      assert.equal(emitterJson["author"], "Test Author");
      assert.equal(emitterJson["license"], "MIT");
      assert.equal(emitterJson["customField"], "customValue");
      assert.exists(emitterJson["scripts"]);
      assert.equal(emitterJson["scripts"]["build"], "tsc");
      assert.equal(emitterJson["scripts"]["test"], "vitest");

      // Check that main field is always set correctly
      assert.equal(emitterJson["main"], "dist/src/index.js");

      // Clean up the temp directory
      await removeDirectory(tempDir);
    } catch (error: any) {
      assert.fail("Failed to generate tsp-client config files. Error: " + error);
    }
  }, 360000);

  it("should read batch configuration from tsp-location.yaml", async () => {
    const tspLocation = await readTspLocation("test/examples/batch");

    expect(tspLocation.batch).toBeDefined();
    expect(Array.isArray(tspLocation.batch)).toBe(true);
    expect(tspLocation.batch).toHaveLength(3);
    expect(tspLocation.batch).toContain("./rbac");
    expect(tspLocation.batch).toContain("./settings");
    expect(tspLocation.batch).toContain("./restore");
  });

  it("process batch directories in updateCommand", async () => {
    const argv = {
      "output-dir": "./test/examples/batch",
    };

    try {
      await updateCommand(argv);

      // Verify that output directories were created for each batch item
      assert.isTrue(
        (await stat(joinPaths(repoRoot, "sdk/keyvault/keyvault-admin/rbac"))).isDirectory(),
      );
      assert.isTrue(
        (await stat(joinPaths(repoRoot, "sdk/keyvault/keyvault-admin/settings"))).isDirectory(),
      );
      assert.isTrue(
        (await stat(joinPaths(repoRoot, "sdk/keyvault/keyvault-admin/restore"))).isDirectory(),
      );
    } finally {
      await removeDirectory(joinPaths(repoRoot, "sdk/keyvault"));
    }
  }, 360000);

  it("process batch directories in updateCommand with local spec path", async () => {
    const argv = {
      "output-dir": "./test/examples/batch",
      "local-spec-repo": "./test/examples/batch/service",
      "emitter-package-json-path": "tools/tsp-client/test/examples/batch/service/package.json",
    };

    try {
      await updateCommand(argv);

      // Verify that output directories were created for each batch item
      assert.isTrue(
        (await stat(joinPaths(repoRoot, "sdk/keyvault/keyvault-admin/rbac"))).isDirectory(),
      );
      assert.isTrue(
        (await stat(joinPaths(repoRoot, "sdk/keyvault/keyvault-admin/settings"))).isDirectory(),
      );
      assert.isTrue(
        (await stat(joinPaths(repoRoot, "sdk/keyvault/keyvault-admin/restore"))).isDirectory(),
      );
    } finally {
      await removeDirectory(joinPaths(repoRoot, "sdk/keyvault"));
    }
  });
});
