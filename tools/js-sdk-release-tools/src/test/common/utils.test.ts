import { describe, expect, test, beforeEach, afterEach } from "vitest";
import {
    resolveOptions,
    specifyApiVersionToGenerateSDKByTypeSpec,
    cleanUpPackageDirectory,
    getPackageNameFromTspConfig,
    getApiReviewPath,
} from "../../common/utils.js";
import path from "path";
import { deepStrictEqual, strictEqual } from "assert";
import * as fs from "fs";
import { isStableSDKReleaseType } from "../../utils/version.js";
import { getRandomInt } from "../utils/utils.js";
import { ensureDir, remove, writeFile, pathExists } from "fs-extra";
import { stringify } from "yaml";
import { RunMode } from "../../common/types.js";
import { mkdir, readdir } from "fs/promises";

describe("resolveOptions", () => {
    test("loads config at the given path", async () => {
        const configPath = path.join(__dirname, "config/myConfig.yaml");
        const emitterOutputDir = path.posix
            .join(__dirname, "/config")
            .replace(/\\/g, "/");
        const options = await resolveOptions(configPath);
        deepStrictEqual(options.options, {
            "@azure-tools/typespec-autorest": {
                "azure-resource-provider-folder": "resource-manager",
                "emitter-output-dir": `${emitterOutputDir}/..`,
                "output-file":
                    "resource-manager/{service-name}/{version-status}/{version}/openapi.json",
            },
            "@azure-tools/typespec-ts": {
                "output-folder": "sdk/informatica/src/generated",
                "package-dir": "op/value",
                "service-dir": "sdk/informatica",
            },
            usingParam: {
                "output-file": "sdk/informaticadatamanagement",
                "output-dir": "params/default/value",
            },
        });
        strictEqual(
            options.configFile.parameters?.["service-dir"]?.default,
            "sdk/informaticadatamanagement",
        );
    });
});

describe("specifiyApiVersionToGenerateSDKByTypeSpec", () => {
    test("Updated API version into tspconfig.yaml", async () => {
        const fakeTspConfig = {
            options: {
                "@azure-tools/typespec-ts": {
                    "is-modular-library": true,
                },
            },
        };
        const tempSpecFolder = path.join(
            __dirname,
            `tmp/spec-${getRandomInt(10000)}`,
        );
        try {
            await ensureDir(tempSpecFolder);
            await writeFile(
                path.join(tempSpecFolder, "tspconfig.yaml"),
                stringify(fakeTspConfig),
                {
                    encoding: "utf8",
                    flush: true,
                },
            );
            const expectedVersion = "2023-10-01";
            specifyApiVersionToGenerateSDKByTypeSpec(
                tempSpecFolder,
                expectedVersion,
            );
            const data: string = fs.readFileSync(
                path.join(tempSpecFolder, "tspconfig.yaml"),
                "utf8",
            );
            expect(data.includes(`api-version: '${expectedVersion}'`)).toBe(
                true,
            );
        } finally {
            await remove(tempSpecFolder);
        }
    });

    test("tspconfig.yaml does not exist", async () => {
        const tempSpecFolder = path.join(
            __dirname,
            `tmp/spec-${getRandomInt(10000)}`,
        );
        try {
            await ensureDir(tempSpecFolder);            
            expect(() =>
                specifyApiVersionToGenerateSDKByTypeSpec(
                    tempSpecFolder,
                    "2023-10-01",
                ),
            ).toThrow(
                `Failed to find tspconfig.yaml in ${tempSpecFolder}.`,
            );
        } finally {
            await remove(tempSpecFolder);
        }
    });

    test("not found @azure-tools/typespec-ts options in tspconfig.yaml", async () => {
        const fakeTspConfig = {
            options: {
                "@azure-tools/typespec-go": {
                    "is-modular-library": true,
                },
            },
        };
        const tempSpecFolder = path.join(
            __dirname,
            `tmp/spec-${getRandomInt(10000)}`,
        );
        try {
            await ensureDir(tempSpecFolder);
            await writeFile(
                path.join(tempSpecFolder, "tspconfig.yaml"),
                stringify(fakeTspConfig),
                {
                    encoding: "utf8",
                    flush: true,
                },
            );
            expect(() =>
                specifyApiVersionToGenerateSDKByTypeSpec(
                    tempSpecFolder,
                    "2023-10-01",
                ),
            ).toThrow(
                `Failed to find @azure-tools/typespec-ts options in tspconfig.yaml.`,
            );
        } finally {
            await remove(tempSpecFolder);
        }
    });

    test("Failed to parse tspconfig.yaml", async () => {    
        const badYaml = `
            skills:
              - JavaScript
              - Node.js
              - YAML
                - extra-indent-error
        `;    
        const tempSpecFolder = path.join(
            __dirname,
            `tmp/spec-${getRandomInt(10000)}`,
        );
        try {
            await ensureDir(tempSpecFolder);
            await writeFile(
                path.join(tempSpecFolder, "tspconfig.yaml"),
                badYaml,
                {
                    encoding: "utf8",
                    flush: true,
                },
            );
            expect(() =>
                specifyApiVersionToGenerateSDKByTypeSpec(
                    tempSpecFolder,
                    "2023-10-01",
                ),
            ).toThrowError('Failed to parse tspconfig.yaml');
        } finally {
            await remove(tempSpecFolder);
        }
    });
});

describe("getReleaseStatus", () => {
    test("apiVersion is stable, sdkReleaseType is stable", async () => {
        const result = await isStableSDKReleaseType("Preview", {
            apiVersion: "2023-10-01",
            sdkReleaseType: "stable",
        });
        expect(result).toBe(true);
    });

    test("apiVersion is stable, sdkReleaseType is beta", async () => {
        const result = await isStableSDKReleaseType("Preview", {
            apiVersion: "2023-10-01",
            sdkReleaseType: "beta",
        });
        expect(result).toBe(false);
    });

    test("apiVersion is preview, sdkReleaseType is stable", async () => {
        const result = await isStableSDKReleaseType("Stable", {
            apiVersion: "2023-10-01-preview",
            sdkReleaseType: "stable",
        });
        expect(result).toBe(true);
    });

    test("apiVersion is preview, sdkReleaseType is beta", async () => {
        const result = await isStableSDKReleaseType("Stable", {
            apiVersion: "2023-10-01-preview",
            sdkReleaseType: "beta",
        });
        expect(result).toBe(false);
    });

    test("apiVersion not be provided", async () => {
        const result = await isStableSDKReleaseType("Preview", {
            apiVersion: "",
            sdkReleaseType: "stable",
        });
        expect(result).toBe(false);
    });
});

describe("cleanUpPackageDirectory", () => {
    async function createTestDirectoryStructure(baseDir: string): Promise<string> {
        const tempPackageDir = path.join(baseDir, `tmp/package-${getRandomInt(10000)}`);
        
        // Create main directories
        await ensureDir(tempPackageDir);
        await ensureDir(path.join(tempPackageDir, "dist"));
        
        // Create src directory with subfolders and files
        await ensureDir(path.join(tempPackageDir, "src"));
        await ensureDir(path.join(tempPackageDir, "src", "common"));
        await ensureDir(path.join(tempPackageDir, "src", "utils"));
        await writeFile(path.join(tempPackageDir, "src", "index.ts"), "export * from './common';\nexport * from './utils';", "utf8");
        await writeFile(path.join(tempPackageDir, "src", "common", "index.ts"), "// Common module exports", "utf8");
        await writeFile(path.join(tempPackageDir, "src", "utils", "helpers.ts"), "// Helper functions", "utf8");
        
        // Create test directory with subfolders and files
        await ensureDir(path.join(tempPackageDir, "test"));
        await ensureDir(path.join(tempPackageDir, "test", "common"));
        await ensureDir(path.join(tempPackageDir, "test", "utils"));
        await writeFile(path.join(tempPackageDir, "test", "index.test.ts"), "import { describe, test } from 'vitest';\n\ndescribe('index', () => {\n  test('exports', () => {});\n});", "utf8");
        await writeFile(path.join(tempPackageDir, "test", "common", "utils.test.ts"), "// Common utils tests", "utf8");
        
        // Create root files
        await writeFile(path.join(tempPackageDir, "assets.json"), "{}", "utf8");
        await writeFile(path.join(tempPackageDir, "package.json"), "{}", "utf8");
        
        return tempPackageDir;
    }
    
    test("preserves test directory and assets.json in non-SpecPullRequest mode", async () => {
        const tempPackageDir = await createTestDirectoryStructure(__dirname);
        
        try {            
            // Run the function with Release mode
            await cleanUpPackageDirectory(tempPackageDir, RunMode.Release);
            
            // Check if test directory and assets.json are preserved
            const testDirExists = await pathExists(path.join(tempPackageDir, "test"));
            const assetsFileExists = await pathExists(path.join(tempPackageDir, "assets.json"));
            const srcDirExists = await pathExists(path.join(tempPackageDir, "src"));
            const packageJsonExists = await pathExists(path.join(tempPackageDir, "package.json"));
            
            // Check if test subfolders and files are preserved
            const testCommonDirExists = await pathExists(path.join(tempPackageDir, "test", "common"));
            const testUtilsDirExists = await pathExists(path.join(tempPackageDir, "test", "utils"));
            const testIndexFileExists = await pathExists(path.join(tempPackageDir, "test", "index.test.ts"));
            const testUtilsFileExists = await pathExists(path.join(tempPackageDir, "test", "common", "utils.test.ts"));
            
            // Assertions for directories and files
            expect(testDirExists).toBe(true);
            expect(testCommonDirExists).toBe(true);
            expect(testUtilsDirExists).toBe(true);
            expect(testIndexFileExists).toBe(true);
            expect(testUtilsFileExists).toBe(true);
            expect(assetsFileExists).toBe(true);

            // Verify removed directories and files
            expect(srcDirExists).toBe(false);
            expect(packageJsonExists).toBe(false);
        } finally {
            await remove(tempPackageDir);
        }
    });
    
    test("removes all files and directories in SpecPullRequest mode", async () => {
        const tempPackageDir = await createTestDirectoryStructure(__dirname);
        
        try {
            // Run the function with SpecPullRequest mode
            await cleanUpPackageDirectory(tempPackageDir, RunMode.SpecPullRequest);

            // Check if everything is removed
            const entries = await readdir(tempPackageDir);
            expect(entries.length).toBe(0);
        } finally {
            await remove(tempPackageDir);
        }
    });
    
    test("removes all files and directories in Batch mode", async () => {
        const tempPackageDir = await createTestDirectoryStructure(__dirname);
        
        try {
            // Run the function with Batch mode
            await cleanUpPackageDirectory(tempPackageDir, RunMode.Batch);

            // Check if everything is removed
            const entries = await readdir(tempPackageDir);
            expect(entries.length).toBe(0);
        } finally {
            await remove(tempPackageDir);
        }
    });

    test("handles empty directory", async () => {
        const tempPackageDir = path.join(
            __dirname,
            `tmp/package-${getRandomInt(10000)}`
        );
        
        try {
            // Create an empty directory
            await ensureDir(tempPackageDir);
            
            // Run the function
            await cleanUpPackageDirectory(tempPackageDir, RunMode.SpecPullRequest);
            
            // Directory should still exist but be empty
            const exists = await pathExists(tempPackageDir);
            const entries = await readdir(tempPackageDir);
            
            expect(exists).toBe(true);
            expect(entries.length).toBe(0);
        } finally {
            await remove(tempPackageDir);
        }
    });

    test("does not create directory if it doesn't exist and doesn't throw exception", async () => {
        const tempBaseDir = path.join(
            __dirname,
            `tmp/base-${getRandomInt(10000)}`
        );
        
        const nonExistentPackageDir = path.join(
            tempBaseDir,
            "non-existent-package"
        );
        
        try {
            // Ensure the base directory exists but not the package directory
            await ensureDir(tempBaseDir);
            
            // Verify the package directory doesn't exist yet
            const existsBeforeCleanup = await pathExists(nonExistentPackageDir);
            expect(existsBeforeCleanup).toBe(false);
            
            // Expect the function to complete without throwing an exception
            await expect(cleanUpPackageDirectory(nonExistentPackageDir, RunMode.Release)).resolves.not.toThrow();
            
            // Verify the directory still doesn't exist after cleanup
            const existsAfterCleanup = await pathExists(nonExistentPackageDir);
            expect(existsAfterCleanup).toBe(false);
        } finally {
            // Clean up the base directory
            await remove(tempBaseDir);
        }
    });
});

describe("getPackageNameFromTspConfig", () => {
    // Store the original function for spying
    const originalResolveOptions = resolveOptions;
    
    // Create an interception wrapper
    let mockConfigForTest;
    const interceptResolveOptions = async (dir) => {
        return mockConfigForTest;
    };
    
    beforeEach(() => {
        // Replace original function with our interceptor
        global.resolveOptions = interceptResolveOptions;
    });
    
    afterEach(() => {
        // Restore original function
        global.resolveOptions = originalResolveOptions;
        mockConfigForTest = undefined;
    });

    // Test utilities
    async function setupTempDirectory() {
        const tempSpecFolder = path.join(
            __dirname,
            `tmp/spec-${getRandomInt(10000)}`
        );
        await ensureDir(tempSpecFolder);
        return tempSpecFolder;
    }

    async function writeTspConfig(tempSpecFolder, config) {
        await writeFile(
            path.join(tempSpecFolder, "tspconfig.yaml"),
            stringify(config),
            { encoding: "utf8" }
        );
    }

    test("extracts package name from package-details.name when it exists", async () => {
        const tempSpecFolder = await setupTempDirectory();
        
        try {
            const tspConfig = {
                parameters: {
                    "package-dir": {
                        default: "sdk/contoso"
                    }
                },
                options: {
                    "@azure-tools/typespec-ts": {
                        "package-dir": "arm-something-else",
                        "package-details": {
                            name: "@azure/arm-contoso"
                        }
                    }
                }
            };
            
            await writeTspConfig(tempSpecFolder, tspConfig);
            
            // Setup mock result for this test
            mockConfigForTest = {
                options: tspConfig.options,
                configFile: {
                    parameters: tspConfig.parameters
                }
            };
            
            // Call function and verify result
            const result = await getPackageNameFromTspConfig(tempSpecFolder);
            expect(result).toBe("@azure/arm-contoso");
        } finally {
            await remove(tempSpecFolder);
        }
    });    

    test("returns undefined when package-details.name doesn't exist", async () => {
        const tempSpecFolder = await setupTempDirectory();
        
        try {
            const tspConfig = {
                parameters: {
                    "package-dir": {
                        default: "arm-contoso"
                    }
                },
                options: {
                    "@azure-tools/typespec-ts": {
                        "package-dir": "arm-contoso"
                    }
                }
            };
            
            await writeTspConfig(tempSpecFolder, tspConfig);
            
            // Setup mock result for this test
            mockConfigForTest = {
                options: tspConfig.options,
                configFile: {
                    parameters: tspConfig.parameters
                }
            };
            
            // Call function and verify result
            const result = await getPackageNameFromTspConfig(tempSpecFolder);
            expect(result).toBeUndefined();
        } finally {
            await remove(tempSpecFolder);
        }
    });   

    test("returns undefined when options are empty", async () => {
        const tempSpecFolder = await setupTempDirectory();
        
        try {
            const tspConfig = {
                parameters: {},
                options: {
                    "@azure-tools/typespec-ts": {}
                }
            };
            
            await writeTspConfig(tempSpecFolder, tspConfig);
            
            // Setup mock result for this test
            mockConfigForTest = {
                options: tspConfig.options,
                configFile: {
                    parameters: tspConfig.parameters
                }
            };
            
            // Call function and verify result
            const result = await getPackageNameFromTspConfig(tempSpecFolder);
            expect(result).toBeUndefined();
        } finally {
            await remove(tempSpecFolder);
        }
    });
});

describe("getApiReviewPath - File Priority Tests", () => {
    let tempDir: string;
    
    beforeEach(async () => {
        tempDir = path.join(__dirname, "temp-getApiReviewPath-test-" + Date.now());
        await ensureDir(tempDir);
    });

    afterEach(async () => {
        await remove(tempDir);
    });

    // Helper function to create a basic package structure
    async function createPackage(packageName: string, isHighLevelClient = false) {
        const packageDir = path.join(tempDir, packageName);
        const reviewDir = path.join(packageDir, "review");
        await ensureDir(reviewDir);
        
        await writeFile(
            path.join(packageDir, "package.json"),
            JSON.stringify({ name: packageName })
        );
        
        // Create parameters.ts for HighLevelClient identification
        if (isHighLevelClient) {
            await ensureDir(path.join(packageDir, "src", "models"));
            await writeFile(
                path.join(packageDir, "src", "models", "parameters.ts"),
                "// parameters file"
            );
        }
        
        return { packageDir, reviewDir };
    }

    test("should prioritize -node.api.md when both files exist", async () => {
        const { packageDir, reviewDir } = await createPackage("@azure/test-package", true);
        
        // Create both API review files
        const standardApiFile = path.join(reviewDir, "test-package.api.md");
        const nodeApiFile = path.join(reviewDir, "test-package-node.api.md");
        
        await writeFile(standardApiFile, "// Standard API content");
        await writeFile(nodeApiFile, "// Node API content");
        
        const result = getApiReviewPath(packageDir);
        
        expect(result).toBe(nodeApiFile);
        expect(result.endsWith("-node.api.md")).toBe(true);
    });

    test("should fallback to standard .api.md when -node.api.md doesn't exist", async () => {
        const { packageDir, reviewDir } = await createPackage("@azure/test-package", true);
        
        // Create only standard API review file
        const standardApiFile = path.join(reviewDir, "test-package.api.md");
        await writeFile(standardApiFile, "// Standard API content");
        
        const result = getApiReviewPath(packageDir);
        
        expect(result).toBe(standardApiFile);
        expect(result.endsWith(".api.md")).toBe(true);
        expect(result.endsWith("-node.api.md")).toBe(false);
    });

    test("should work with different package types (Modular, Rest, HLC)", async () => {
        // Test with ModularClient package (no parameters.ts)
        const { packageDir: modularDir, reviewDir: modularReview } = await createPackage("@azure/modular-package");
        const nodeApiFile = path.join(modularReview, "modular-package-node.api.md");
        await writeFile(nodeApiFile, "// Modular Node API content");
        
        const modularResult = getApiReviewPath(modularDir);
        expect(modularResult).toBe(nodeApiFile);
        
        // Test with RestLevelClient package
        const { packageDir: restDir, reviewDir: restReview } = await createPackage("@azure-rest/rest-package");
        const standardApiFile = path.join(restReview, "rest-package.api.md");
        await writeFile(standardApiFile, "// Rest API content");
        
        const restResult = getApiReviewPath(restDir);
        expect(restResult).toBe(standardApiFile);
    });
});
