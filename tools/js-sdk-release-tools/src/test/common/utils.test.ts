import { describe, expect, test, beforeEach, afterEach } from "vitest";
import {
    resolveOptions,
    specifyApiVersionToGenerateSDKByTypeSpec,
    cleanUpPackageDirectory,
    getPackageNameFromTspConfig,
    applyLegacySettingsMapping,
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

describe("applyLegacySettingsMapping", () => {
    let tempDir: string;
    
    beforeEach(async () => {
        // Create a temporary directory for test files
        tempDir = path.join(__dirname, `temp-${getRandomInt(10000)}`);
        await ensureDir(tempDir);
    });

    afterEach(async () => {
        // Clean up temporary directory
        if (await pathExists(tempDir)) {
            await remove(tempDir);
        }
    });

    test("should not modify tspconfig when mapping is disabled", async () => {
        const tspConfigContent = {
            options: {
                "@azure-tools/typespec-ts": {
                    "generateTest": true,
                    "packageDetails": { name: "@azure/test" }
                }
            }
        };

        const tspConfigPath = path.join(tempDir, "tspconfig.yaml");
        await writeFile(tspConfigPath, stringify(tspConfigContent));

        // Get initial content
        const initialContent = fs.readFileSync(tspConfigPath, 'utf8');

        await applyLegacySettingsMapping(tempDir, false);
        
        // Content should remain unchanged
        const finalContent = fs.readFileSync(tspConfigPath, 'utf8');
        expect(finalContent).toBe(initialContent);
    });

    test("should map legacy TypeSpec emitter options when mapping is enabled", async () => {
        const tspConfigContent = {
            options: {
                "@azure-tools/typespec-ts": {
                    "generateTest": true,
                    "packageDetails": { name: "@azure/test" },
                    "generateMetadata": false
                }
            }
        };

        const tspConfigPath = path.join(tempDir, "tspconfig.yaml");
        await writeFile(tspConfigPath, stringify(tspConfigContent));

        await applyLegacySettingsMapping(tempDir, true);

        // Read the updated file and parse as YAML
        const updatedContent = fs.readFileSync(tspConfigPath, 'utf8');
        const { parse } = await import('yaml');
        const updatedConfig = parse(updatedContent);

        // Check that TypeSpec emitter options are mapped
        const emitterOptions = updatedConfig.options["@azure-tools/typespec-ts"];
        expect(emitterOptions["generate-test"]).toBe(true);
        expect(emitterOptions["package-details"]).toEqual({ name: "@azure/test" });
        expect(emitterOptions["generate-metadata"]).toBe(false);
        
        // Check that old options are removed
        expect(emitterOptions["generateTest"]).toBeUndefined();
        expect(emitterOptions["packageDetails"]).toBeUndefined();
        expect(emitterOptions["generateMetadata"]).toBeUndefined();
    });

    test("should not overwrite existing new options", async () => {
        const tspConfigContent = {
            options: {
                "@azure-tools/typespec-ts": {
                    "generateTest": true,
                    "generate-test": false
                }
            }
        };

        const tspConfigPath = path.join(tempDir, "tspconfig.yaml");
        await writeFile(tspConfigPath, stringify(tspConfigContent));

        await applyLegacySettingsMapping(tempDir, true);

        // Read the updated file and parse as YAML
        const updatedContent = fs.readFileSync(tspConfigPath, 'utf8');
        const { parse } = await import('yaml');
        const updatedConfig = parse(updatedContent);

        // New option should be preserved, old one should remain unchanged since new one exists
        const emitterOptions = updatedConfig.options["@azure-tools/typespec-ts"];
        expect(emitterOptions["generate-test"]).toBe(false);
        expect(emitterOptions["generateTest"]).toBe(true);
    });

    test("should handle options not in built-in mapping", async () => {
        const tspConfigContent = {
            options: {
                "@azure-tools/typespec-ts": {
                    "customOldKey": "custom-value",
                    "generateTest": true
                }
            }
        };

        const tspConfigPath = path.join(tempDir, "tspconfig.yaml");
        await writeFile(tspConfigPath, stringify(tspConfigContent));

        await applyLegacySettingsMapping(tempDir, true);

        // Read the updated file and parse as YAML
        const updatedContent = fs.readFileSync(tspConfigPath, 'utf8');
        const { parse } = await import('yaml');
        const updatedConfig = parse(updatedContent);

        // Built-in mapping should work
        const emitterOptions = updatedConfig.options["@azure-tools/typespec-ts"];
        expect(emitterOptions["generate-test"]).toBe(true);
        expect(emitterOptions["generateTest"]).toBeUndefined();
        
        // Custom key not in mapping should remain unchanged
        expect(emitterOptions["customOldKey"]).toBe("custom-value");
    });

    test("should generate package-dir from packageDetails.name when package-dir doesn't exist", async () => {
        const tspConfigContent = {
            options: {
                "@azure-tools/typespec-ts": {
                    "packageDetails": {
                        "name": "@azure/ai-agents"
                    }
                }
            }
        };

        const tspConfigPath = path.join(tempDir, "tspconfig.yaml");
        await writeFile(tspConfigPath, stringify(tspConfigContent));

        await applyLegacySettingsMapping(tempDir, true);

        // Read the updated file and parse as YAML
        const updatedContent = fs.readFileSync(tspConfigPath, 'utf8');
        const { parse } = await import('yaml');
        const updatedConfig = parse(updatedContent);

        // packageDetails should be mapped to package-details
        const emitterOptions = updatedConfig.options["@azure-tools/typespec-ts"];
        expect(emitterOptions["package-details"]).toBeDefined();
        expect(emitterOptions["packageDetails"]).toBeUndefined();
        // package-dir should be generated from package-details.name
        expect(emitterOptions["package-dir"]).toBe("ai-agents");
    });

    test("should generate package-dir from package-details.name when package-dir doesn't exist", async () => {
        const tspConfigContent = {
            options: {
                "@azure-tools/typespec-ts": {
                    "package-details": {
                        "name": "@azure/storage-blob"
                    }
                }
            }
        };

        const tspConfigPath = path.join(tempDir, "tspconfig.yaml");
        await writeFile(tspConfigPath, stringify(tspConfigContent));

        await applyLegacySettingsMapping(tempDir, true);

        // Read the updated file and parse as YAML
        const updatedContent = fs.readFileSync(tspConfigPath, 'utf8');
        const { parse } = await import('yaml');
        const updatedConfig = parse(updatedContent);

        const emitterOptions = updatedConfig.options["@azure-tools/typespec-ts"];
        expect(emitterOptions["package-dir"]).toBe("storage-blob");
    });

    test("should not override existing package-dir", async () => {
        const tspConfigContent = {
            options: {
                "@azure-tools/typespec-ts": {
                    "package-dir": "existing-dir",
                    "packageDetails": {
                        "name": "@azure/ai-agents"
                    }
                }
            }
        };

        const tspConfigPath = path.join(tempDir, "tspconfig.yaml");
        await writeFile(tspConfigPath, stringify(tspConfigContent));

        await applyLegacySettingsMapping(tempDir, true);

        // Read the updated file and parse as YAML
        const updatedContent = fs.readFileSync(tspConfigPath, 'utf8');
        const { parse } = await import('yaml');
        const updatedConfig = parse(updatedContent);

        // package-dir should remain unchanged
        const emitterOptions = updatedConfig.options["@azure-tools/typespec-ts"];
        expect(emitterOptions["package-dir"]).toBe("existing-dir");
        // packageDetails should be mapped to package-details
        expect(emitterOptions["package-details"]).toBeDefined();
        expect(emitterOptions["packageDetails"]).toBeUndefined();
    });

    test("should handle packageDetails.name without @azure/ prefix", async () => {
        const tspConfigContent = {
            options: {
                "@azure-tools/typespec-ts": {
                    "packageDetails": {
                        "name": "some-other-package"
                    }
                }
            }
        };

        const tspConfigPath = path.join(tempDir, "tspconfig.yaml");
        await writeFile(tspConfigPath, stringify(tspConfigContent));

        await applyLegacySettingsMapping(tempDir, true);

        // Read the updated file and parse as YAML
        const updatedContent = fs.readFileSync(tspConfigPath, 'utf8');
        const { parse } = await import('yaml');
        const updatedConfig = parse(updatedContent);

        // packageDetails should be mapped to package-details
        const emitterOptions = updatedConfig.options["@azure-tools/typespec-ts"];
        expect(emitterOptions["package-details"]).toBeDefined();
        expect(emitterOptions["packageDetails"]).toBeUndefined();
        // package-dir should not be generated since name doesn't start with @azure/
        expect(emitterOptions["package-dir"]).toBeUndefined();
    });

    test("should handle missing packageDetails.name", async () => {
        const tspConfigContent = {
            options: {
                "@azure-tools/typespec-ts": {
                    "packageDetails": {}
                }
            }
        };

        const tspConfigPath = path.join(tempDir, "tspconfig.yaml");
        await writeFile(tspConfigPath, stringify(tspConfigContent));

        await applyLegacySettingsMapping(tempDir, true);

        // Read the updated file and parse as YAML
        const updatedContent = fs.readFileSync(tspConfigPath, 'utf8');
        const { parse } = await import('yaml');
        const updatedConfig = parse(updatedContent);

        // packageDetails should be mapped to package-details
        const emitterOptions = updatedConfig.options["@azure-tools/typespec-ts"];
        expect(emitterOptions["package-details"]).toBeDefined();
        expect(emitterOptions["packageDetails"]).toBeUndefined();
        // package-dir should not be generated since name is missing
        expect(emitterOptions["package-dir"]).toBeUndefined();
    });

    test("should only modify @azure-tools/typespec-ts emitter and preserve other emitters", async () => {
        const tspConfigContent = {
            options: {
                "@azure-tools/typespec-ts": {
                    "generateTest": true,
                    "packageDetails": { name: "@azure/test" },
                    "generateMetadata": false
                },
                "@azure-tools/typespec-autorest": {
                    "azure-resource-provider-folder": "resource-manager",
                    "output-file": "resource-manager/{service-name}/{version-status}/{version}/openapi.json"
                },
                "someOtherEmitter": {
                    "customOption": "value",
                    "anotherOption": { nested: "data" }
                }
            }
        };

        const tspConfigPath = path.join(tempDir, "tspconfig.yaml");
        await writeFile(tspConfigPath, stringify(tspConfigContent));

        await applyLegacySettingsMapping(tempDir, true);

        // Read the updated file and parse as YAML
        const updatedContent = fs.readFileSync(tspConfigPath, 'utf8');
        const { parse } = await import('yaml');
        const updatedConfig = parse(updatedContent);

        // Check that TypeSpec emitter options are mapped
        const tsEmitterOptions = updatedConfig.options["@azure-tools/typespec-ts"];
        expect(tsEmitterOptions["generate-test"]).toBe(true);
        expect(tsEmitterOptions["package-details"]).toEqual({ name: "@azure/test" });
        expect(tsEmitterOptions["generate-metadata"]).toBe(false);
        
        // Check that old TypeSpec options are removed
        expect(tsEmitterOptions["generateTest"]).toBeUndefined();
        expect(tsEmitterOptions["packageDetails"]).toBeUndefined();
        expect(tsEmitterOptions["generateMetadata"]).toBeUndefined();

        // Check that other emitters remain completely unchanged
        const autorestEmitterOptions = updatedConfig.options["@azure-tools/typespec-autorest"];
        expect(autorestEmitterOptions).toEqual({
            "azure-resource-provider-folder": "resource-manager",
            "output-file": "resource-manager/{service-name}/{version-status}/{version}/openapi.json"
        });

        const otherEmitterOptions = updatedConfig.options["someOtherEmitter"];
        expect(otherEmitterOptions).toEqual({
            "customOption": "value",
            "anotherOption": { nested: "data" }
        });
    });
});
