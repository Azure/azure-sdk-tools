import { mkdir, readFile, rm } from "node:fs/promises";
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { cwd } from "node:process";
import { joinPaths } from "@typespec/compiler";
import { doesFileExist } from "../src/network.js";

// Mock npm.js to control npmViewPackageDevDependencies and avoid real npm install/lock generation
vi.mock("../src/npm.js", async (importOriginal) => {
  const actual = (await importOriginal()) as Record<string, any>;
  return {
    ...actual,
    npmViewPackageDevDependencies: vi.fn(),
    npmCommand: vi.fn().mockResolvedValue(undefined),
  };
});

// Import after vi.mock so the mock is in place
import { generateConfigFilesCommand } from "../src/commands.js";
import { npmViewPackageDevDependencies } from "../src/npm.js";

const mockedNpmView = vi.mocked(npmViewPackageDevDependencies);

/**
 * Helper to call generateConfigFilesCommand and ignore lock-file generation errors.
 * The mocked npmCommand doesn't create a real package-lock.json, so the stat() call
 * in generateLockFileCommandCore will fail. The emitter-package.json is written before
 * that step, which is what we're testing.
 */
async function runGenerateConfigFiles(args: Record<string, any>): Promise<void> {
  try {
    await generateConfigFilesCommand(args);
  } catch {
    // Expected: lock file stat fails because npmCommand is mocked
  }
}

describe("use-npm-pinning flag", () => {
  let tmpDir: string;
  let emitterPackageJsonPath: string;

  beforeEach(async () => {
    tmpDir = joinPaths(cwd(), ".tmp-test-npm-pinning-" + Date.now());
    await mkdir(tmpDir, { recursive: true });
    emitterPackageJsonPath = joinPaths(tmpDir, "emitter-package.json");
    vi.clearAllMocks();
  });

  afterEach(async () => {
    if (await doesFileExist(tmpDir)) {
      await rm(tmpDir, { recursive: true });
    }
  });

  it("calls npmViewPackageDevDependencies when use-npm-pinning is true", async () => {
    mockedNpmView.mockResolvedValue({
      "@typespec/compiler": "1.6.0",
      "@typespec/http": "1.6.0",
    });

    const args = {
      "package-json": joinPaths(cwd(), "test", "examples", "package.json"),
      "emitter-package-json-path": emitterPackageJsonPath,
      "use-npm-pinning": true,
    };

    await runGenerateConfigFiles(args);

    expect(mockedNpmView).toHaveBeenCalledOnce();
    expect(mockedNpmView).toHaveBeenCalledWith("@azure-tools/typespec-ts", "0.46.1");

    const emitterJson = JSON.parse(await readFile(emitterPackageJsonPath, "utf8"));
    expect(emitterJson["devDependencies"]["@typespec/compiler"]).toBe("1.6.0");
    expect(emitterJson["devDependencies"]["@typespec/http"]).toBe("1.6.0");
  });

  it("uses npm-returned versions instead of local devDependencies when use-npm-pinning is true", async () => {
    // Return versions deliberately different from the local package.json devDependencies
    mockedNpmView.mockResolvedValue({
      "@typespec/compiler": "0.99.0-npm-resolved",
      "@azure-tools/typespec-azure-core": "0.55.0-npm-resolved",
    });

    const args = {
      "package-json": joinPaths(cwd(), "test", "examples", "package.json"),
      "emitter-package-json-path": emitterPackageJsonPath,
      "use-npm-pinning": true,
    };

    await runGenerateConfigFiles(args);

    const emitterJson = JSON.parse(await readFile(emitterPackageJsonPath, "utf8"));
    // These should be the npm-resolved values, NOT the local package.json values (1.6.0, 0.62.0)
    expect(emitterJson["devDependencies"]["@typespec/compiler"]).toBe("0.99.0-npm-resolved");
    expect(emitterJson["devDependencies"]["@azure-tools/typespec-azure-core"]).toBe(
      "0.55.0-npm-resolved",
    );
  });

  it("does not call npmViewPackageDevDependencies when use-npm-pinning is not set", async () => {
    const args = {
      "package-json": joinPaths(cwd(), "test", "examples", "package.json"),
      "emitter-package-json-path": emitterPackageJsonPath,
    };

    await runGenerateConfigFiles(args);

    expect(mockedNpmView).not.toHaveBeenCalled();

    const emitterJson = JSON.parse(await readFile(emitterPackageJsonPath, "utf8"));
    // Should use local devDependencies from the package.json fixture
    expect(emitterJson["devDependencies"]["@typespec/compiler"]).toBe("1.6.0");
  });

  it("does not call npmViewPackageDevDependencies when use-npm-pinning is false", async () => {
    const args = {
      "package-json": joinPaths(cwd(), "test", "examples", "package.json"),
      "emitter-package-json-path": emitterPackageJsonPath,
      "use-npm-pinning": false,
    };

    await runGenerateConfigFiles(args);

    expect(mockedNpmView).not.toHaveBeenCalled();

    const emitterJson = JSON.parse(await readFile(emitterPackageJsonPath, "utf8"));
    // Should use local devDependencies from the package.json fixture
    expect(emitterJson["devDependencies"]["@typespec/compiler"]).toBe("1.6.0");
  });

  it("falls back to empty deps when use-npm-pinning is true but npm view returns undefined", async () => {
    mockedNpmView.mockResolvedValue(undefined);

    const args = {
      "package-json": joinPaths(cwd(), "test", "examples", "package.json"),
      "emitter-package-json-path": emitterPackageJsonPath,
      "use-npm-pinning": true,
    };

    await runGenerateConfigFiles(args);

    expect(mockedNpmView).toHaveBeenCalledOnce();

    const emitterJson = JSON.parse(await readFile(emitterPackageJsonPath, "utf8"));
    // npm view returned undefined → localDevDeps = {} → no devDependencies pinned
    expect(emitterJson["devDependencies"]).toBeUndefined();
    // Main dependency should still be set
    expect(emitterJson["dependencies"]["@azure-tools/typespec-ts"]).toBe("0.46.1");
  });

  it("respects azure-sdk/emitter-package-json-pinning when use-npm-pinning is true", async () => {
    // The package-sdk-pinning.json fixture limits pinning to only @typespec/compiler
    // and @azure-tools/typespec-azure-core
    mockedNpmView.mockResolvedValue({
      "@typespec/compiler": "0.66.0",
      "@azure-tools/typespec-azure-core": "0.52.0",
      "@typespec/http": "0.67.0",
      "@typespec/rest": "0.67.0",
    });

    const args = {
      "package-json": joinPaths(cwd(), "test", "examples", "package-sdk-pinning.json"),
      "emitter-package-json-path": emitterPackageJsonPath,
      "use-npm-pinning": true,
    };

    await runGenerateConfigFiles(args);

    const emitterJson = JSON.parse(await readFile(emitterPackageJsonPath, "utf8"));
    // Only packages in azure-sdk/emitter-package-json-pinning should be pinned
    expect(emitterJson["devDependencies"]["@typespec/compiler"]).toBe("0.66.0");
    expect(emitterJson["devDependencies"]["@azure-tools/typespec-azure-core"]).toBe("0.52.0");
    // These are NOT in the pinning list, so they should not appear
    expect(emitterJson["devDependencies"]["@typespec/http"]).toBeUndefined();
    expect(emitterJson["devDependencies"]["@typespec/rest"]).toBeUndefined();
    expect(Object.keys(emitterJson["devDependencies"]).length).toBe(2);
  });

  it("overrides take precedence over npm-resolved versions when use-npm-pinning is true", async () => {
    mockedNpmView.mockResolvedValue({
      "@typespec/compiler": "1.6.0",
      "@typespec/http": "1.6.0",
    });

    const args = {
      "package-json": joinPaths(cwd(), "test", "examples", "package.json"),
      "emitter-package-json-path": emitterPackageJsonPath,
      overrides: joinPaths(cwd(), "test", "examples", "overrides.json"),
      "use-npm-pinning": true,
    };

    await runGenerateConfigFiles(args);

    const emitterJson = JSON.parse(await readFile(emitterPackageJsonPath, "utf8"));
    // overrides.json contains {"prettier": "3.5.3"} which should appear in overrides
    expect(emitterJson["overrides"]).toBeDefined();
    expect(emitterJson["overrides"]["prettier"]).toBe("3.5.3");
    // npm-resolved pinned versions should still be present
    expect(emitterJson["devDependencies"]["@typespec/compiler"]).toBe("1.6.0");
    expect(emitterJson["devDependencies"]["@typespec/http"]).toBe("1.6.0");
  });
});
