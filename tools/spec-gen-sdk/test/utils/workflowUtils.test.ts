import { describe, it, expect, vi, beforeEach } from "vitest";
import { readFileSync } from "fs";
import * as winston from "winston";
import {
    setFailureType,
    getLanguageByRepoName,
    loadConfigContent,
    getSdkRepoConfig,
} from "../../src/utils/workflowUtils";
import {
    FailureType,
    WorkflowContext,
    SdkAutoOptions,
} from "../../src/types/Workflow";
import { SpecConfig, SdkRepoConfig } from "../../src/types/SpecConfig";
import { toolError } from "../../src/utils/messageUtils";
import { getRepoKey } from "../../src/utils/repo";

vi.mock("fs");
vi.mock("../../src/utils/messageUtils");
vi.mock("../../src/utils/repo");

describe("workflowUtils", () => {
    beforeEach(() => {
        vi.clearAllMocks();
    });

    describe("setFailureType", () => {
        it("should set failure type when current failure type is not CodegenFailed", () => {
            const context = { failureType: undefined } as WorkflowContext;
            setFailureType(context, FailureType.SpecGenSdkFailed);
            expect(context.failureType).toBe(FailureType.SpecGenSdkFailed);
        });

        it("should not set failure type when current failure type is CodegenFailed", () => {
            const context: WorkflowContext = {
                failureType: FailureType.CodegenFailed,
            } as WorkflowContext;
            setFailureType(context, FailureType.SpecGenSdkFailed);
            expect(context.failureType).toBe(FailureType.CodegenFailed);
        });
    });

    describe("getLanguageByRepoName", () => {
        it('should return "unknown" for empty or undefined repo name', () => {
            expect(getLanguageByRepoName("")).toBe("unknown");
            expect(
                getLanguageByRepoName((undefined as unknown) as string)
            ).toBe("unknown");
        });

        const testCases = [
            {
                pattern: "js",
                expected: "JavaScript",
                examples: [
                    "azure-sdk-for-js",
                    "microsoft-js-repo",
                    "some-js-library",
                ],
            },
            {
                pattern: "go",
                expected: "Go",
                examples: [
                    "azure-sdk-for-go",
                    "microsoft-go-repo",
                    "some-go-library",
                ],
            },
            {
                pattern: "net",
                expected: ".Net",
                examples: [
                    "azure-sdk-for-net",
                    "microsoft-net-repo",
                    "some-net-library",
                ],
            },
            {
                pattern: "java",
                expected: "Java",
                examples: [
                    "azure-sdk-for-java",
                    "microsoft-java-repo",
                    "some-java-library",
                ],
            },
            {
                pattern: "python",
                expected: "Python",
                examples: [
                    "azure-sdk-for-python",
                    "microsoft-python-repo",
                    "some-python-library",
                ],
            },
        ];

        testCases.forEach(({ pattern, expected, examples }) => {
            it(`should detect ${expected} repositories`, () => {
                examples.forEach((repoName) => {
                    expect(getLanguageByRepoName(repoName)).toBe(expected);
                });
            });
        });

        it("should return original name for unknown language patterns", () => {
            const unknownRepos = [
                "azure-sdk-for-rust",
                "microsoft-cpp-repo",
                "some-unknown-repo",
            ];
            unknownRepos.forEach((repoName) => {
                expect(getLanguageByRepoName(repoName)).toBe(repoName);
            });
        });
    });

    describe("loadConfigContent", () => {
        const mockLogger = ({
            info: vi.fn(),
            error: vi.fn(),
        } as any) as winston.Logger;

        it("should load and parse config file successfully", () => {
            const mockConfig = { key: "value" };
            vi.mocked(readFileSync).mockReturnValue(JSON.stringify(mockConfig));

            const result = loadConfigContent("config.json", mockLogger);

            expect(mockLogger.info).toHaveBeenCalledWith(
                "Load config file: config.json"
            );
            expect(readFileSync).toHaveBeenCalledWith("config.json");
            expect(result).toEqual(mockConfig);
        });

        it("should throw error when file reading fails", () => {
            const error = new Error("File not found");
            vi.mocked(readFileSync).mockImplementation(() => {
                throw error;
            });
            vi.mocked(toolError).mockReturnValue("Tool error message");

            expect(() => loadConfigContent("config.json", mockLogger)).toThrow(
                error
            );
            expect(mockLogger.error).toHaveBeenCalledWith("Tool error message");
        });

        it("should throw error when JSON parsing fails", () => {
            vi.mocked(readFileSync).mockReturnValue("invalid json");
            vi.mocked(toolError).mockReturnValue("Tool error message");

            expect(() =>
                loadConfigContent("config.json", mockLogger)
            ).toThrow();
            expect(mockLogger.error).toHaveBeenCalledWith("Tool error message");
        });
    });

    describe("getSdkRepoConfig", () => {
        const mockGetRepoKey = vi.mocked(getRepoKey);

        beforeEach(() => {
            mockGetRepoKey.mockImplementation((repo: any) =>
                typeof repo === "string" ? { name: repo, owner: "" } : repo
            );
        });

        it("should get SDK repo config for non-PR repo", async () => {
            const options: SdkAutoOptions = {
                specRepo: { owner: "Azure", name: "azure-rest-api-specs" },
                sdkName: "azure-sdk-for-js",
            } as SdkAutoOptions;

            const specRepoConfig: SpecConfig = {
              sdkRepositoryMappings: {
                "azure-sdk-for-js": {
                  mainRepository: {
                    owner: "Azure",
                    name: "azure-sdk-for-js",
                  },
                },
              },
            } as unknown as SpecConfig;

            const result = await getSdkRepoConfig(options, specRepoConfig);

            expect(result.mainBranch).toBe("main");
            expect(result.integrationBranchPrefix).toBe("sdkAutomation");
            expect(result.configFilePath).toBe("swagger_to_sdk_config.json");
        });

        it("should get SDK repo config for PR repo with overrides", async () => {
            const options: SdkAutoOptions = {
                specRepo: { owner: "Azure", name: "azure-rest-api-specs-pr" },
                sdkName: "azure-sdk-for-js",
            } as SdkAutoOptions;

            const specRepoConfig: SpecConfig = {
              sdkRepositoryMappings: {},
              overrides: {
                "Azure/azure-rest-api-specs-pr": {
                  sdkRepositoryMappings: {
                    "azure-sdk-for-js": {
                      mainRepository: {
                        owner: "Azure",
                        name: "azure-sdk-for-js",
                      },
                    }
                  },
                },
              },
            } as unknown as SpecConfig;

            const result = await getSdkRepoConfig(options, specRepoConfig);

            expect(result).toBeDefined();
        });

        it("should handle string SDK repo config", async () => {
            const options: SdkAutoOptions = {
                specRepo: { owner: "Azure", name: "azure-rest-api-specs" },
                sdkName: "azure-sdk-for-js",
            } as SdkAutoOptions;

            const specRepoConfig: SpecConfig = {
              sdkRepositoryMappings: {
                "azure-sdk-for-js": "azure-sdk-for-js",
              },
            } as unknown as SpecConfig;

            const result = await getSdkRepoConfig(options, specRepoConfig);

            expect(result.mainRepository).toBeDefined();
            expect(result.mainBranch).toBe("main");
        });

        it("should throw error when SDK repository mappings not found", async () => {
            const options: SdkAutoOptions = {
                specRepo: { owner: "Azure", name: "azure-rest-api-specs" },
                sdkName: "azure-sdk-for-js",
            } as SdkAutoOptions;

            const specRepoConfig: SpecConfig = {} as SpecConfig;

            vi.mocked(toolError).mockReturnValue("Tool error message");

            await expect(
                getSdkRepoConfig(options, specRepoConfig)
            ).rejects.toThrow("Tool error message");
        });

        it("should throw error when SDK is not defined in config", async () => {
            const options: SdkAutoOptions = {
                specRepo: { owner: "Azure", name: "azure-rest-api-specs" },
                sdkName: "non-existent-sdk",
            } as SdkAutoOptions;

            const specRepoConfig: SpecConfig = {
                sdkRepositoryMappings: {},
            } as SpecConfig;

            vi.mocked(toolError).mockReturnValue("Tool error message");

            await expect(
                getSdkRepoConfig(options, specRepoConfig)
            ).rejects.toThrow("Tool error message");
        });

        it("should set default values for optional config properties", async () => {
            const options: SdkAutoOptions = {
                specRepo: { owner: "Azure", name: "azure-rest-api-specs" },
                sdkName: "azure-sdk-for-js",
            } as SdkAutoOptions;

            const specRepoConfig: SpecConfig = {
              sdkRepositoryMappings: {
                "azure-sdk-for-js": {
                  mainRepository: {
                    owner: "Azure",
                    name: "azure-sdk-for-js",
                  },
                },
              },
            } as unknown as SpecConfig;

            const result = await getSdkRepoConfig(options, specRepoConfig);

            expect(result.mainBranch).toBe("main");
            expect(result.integrationBranchPrefix).toBe("sdkAutomation");
            expect(result.secondaryBranch).toBe("main");
            expect(result.configFilePath).toBe("swagger_to_sdk_config.json");
        });
    });
});
