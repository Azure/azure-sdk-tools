import { describe, expect, test, vi, beforeEach, afterEach } from "vitest";
import * as npmUtilsModule from "../../../common/npmUtils.js";
import * as utilsModule from "../../../common/utils.js";
import * as fsModule from "fs";
import * as pathModule from "path";
import shellModule from "shelljs";
import * as codeOwnersModule from "../../../common/codeOwnersAndIgnoreLink/codeOwnersAndIgnoreLinkGenerator.js";

// Create module mocks
vi.mock("../../../common/npmUtils.js");
vi.mock("../../../common/utils.js");
vi.mock("fs");
vi.mock("path");
vi.mock("shelljs");

// Mock data for files
const mockCODEOWNERSContent = `
# This file represents the code owners of the repository
# Each line is a file pattern followed by one or more owners

###########
# SDK
###########

# Catch all
/sdk/ @xirzec @Azure/azure-sdk-js-dev

###########
# Config
###########
/.vscode/ @mikeharder @deyaaeldeen @jeremymeng`;

const mockIgnoreLinksContent = `
https://learn.microsoft.com/javascript/api/@azure/arm-kubernetesconfiguration-extensiontypes?view=azure-node-preview\n
https://learn.microsoft.com/javascript/api/@azure/arm-sitemanager?view=azure-node-preview`;

describe("generateCodeOwnersAndIgnoreLinkForPackage", () => {   
    // Mock paths
    const mockSdkRepoPath = "/mock/sdk/repo/path";
    const mockPackageFolderPath = "sdk/test-package";
    const mockPackageAbsolutePath = "/mock/sdk/repo/path/sdk/test-package";
    const mockPackageName = "@azure/mock-package";
    const mockCodeOwnersPath = "/mock/sdk/repo/path/.github/CODEOWNERS";
    const mockIgnoreLinksPath = "/mock/sdk/repo/path/eng/ignore-links.txt";

    beforeEach(() => {
        // Reset all mocks
        vi.resetAllMocks();
        // Setup mock for shell.pwd()
        vi.mocked(shellModule.pwd).mockReturnValue({
            toString: () => mockSdkRepoPath,
        } as any);

        // Setup mock for path.join
        vi.mocked(pathModule.join).mockImplementation((...paths) => {
            if (
                paths[0] === mockSdkRepoPath &&
                paths[1] === mockPackageFolderPath
            ) {
                return mockPackageAbsolutePath;
            }
            if (
                paths[0] === mockSdkRepoPath &&
                paths[1] === ".github" &&
                paths[2] === "CODEOWNERS"
            ) {
                return mockCodeOwnersPath;
            }
            if (
                paths[0] === mockSdkRepoPath &&
                paths[1] === "eng" &&
                paths[2] === "ignore-links.txt"
            ) {
                return mockIgnoreLinksPath;
            }
            return paths.join("/");
        });

        // Setup mock for fs.readFileSync
        vi.mocked(fsModule.readFileSync).mockImplementation((path, options) => {
            if (path === mockCodeOwnersPath) {
                return mockCODEOWNERSContent;
            }
            if (path === mockIgnoreLinksPath) {
                return mockIgnoreLinksContent;
            }
            return "";
        });

        // Setup mock for getNpmPackageName
        vi.mocked(utilsModule.getNpmPackageName).mockReturnValue(
            mockPackageName,
        );
    });

    afterEach(() => {
        vi.resetAllMocks();
    });

    test("should not update files if package already exists in npm", async () => {
        // Setup mock for tryGetNpmView to return a non-undefined value (package exists)
        vi.mocked(npmUtilsModule.tryGetNpmView).mockResolvedValue({
            version: "1.0.0",
        });

        // Call the function
        await codeOwnersModule.tryGenerateCodeOwnersAndIgnoreLinkForPackage(
            mockPackageFolderPath,
            mockPackageName
        );

        // Check that fs.writeFileSync was not called
        expect(fsModule.writeFileSync).not.toHaveBeenCalled();
    });

    test("should update both CODEOWNERS and ignore-links.txt for first beta release", async () => {
        // Setup mock for tryGetNpmView to return undefined (package doesn't exist)
        vi.mocked(npmUtilsModule.tryGetNpmView).mockResolvedValue(undefined);
        // Call the function
        await codeOwnersModule.tryGenerateCodeOwnersAndIgnoreLinkForPackage(
            mockPackageFolderPath,
            mockPackageName
        );

        // Check that fs.writeFileSync was called twice (for CODEOWNERS and ignore-links.txt)
        expect(fsModule.writeFileSync).toHaveBeenCalledTimes(2);

        // Check CODEOWNERS update
        const newContentBeforeConfig = `# PRLabel: %Mgmt\n${mockPackageFolderPath}/ @qiaozha @MaryGao @JialinHuang803\n`;
        const configSectionIndex = mockCODEOWNERSContent.indexOf(
            "###########\n# Config\n###########",
        );
        const expectedCODEOWNERSContent =
            mockCODEOWNERSContent.slice(0, configSectionIndex) +
            newContentBeforeConfig +
            "\n" +
            mockCODEOWNERSContent.slice(configSectionIndex);

        expect(fsModule.writeFileSync).toHaveBeenNthCalledWith(
            1,
            mockCodeOwnersPath,
            expectedCODEOWNERSContent,
        );

        // Check ignore-links.txt update
        const newIgnoreLink = `https://learn.microsoft.com/javascript/api/${mockPackageName}?view=azure-node-preview`;
        // Check if mockIgnoreLinksContent already ends with a newline
        let expectedIgnoreLinksContent = mockIgnoreLinksContent;
        if (!expectedIgnoreLinksContent.endsWith("\n")) {
            expectedIgnoreLinksContent += "\n";
        }
        expectedIgnoreLinksContent += newIgnoreLink + "\n";

        expect(fsModule.writeFileSync).toHaveBeenNthCalledWith(
            2,
            mockIgnoreLinksPath,
            expectedIgnoreLinksContent,
        );
    });

    test("should not add duplicate entry to CODEOWNERS if entry already exists", async () => {
        // Setup mock for tryGetNpmView to return undefined (package doesn't exist)
        vi.mocked(npmUtilsModule.tryGetNpmView).mockResolvedValue(undefined);

        // Modify mockCODEOWNERSContent to include the entry already
        const newContentBeforeConfig = `# PRLabel: %Mgmt\n${mockPackageFolderPath}/ @qiaozha @MaryGao @JialinHuang803\n`;
        const configSectionIndex = mockCODEOWNERSContent.indexOf(
            "###########\n# Config\n###########",
        );
        const contentWithExistingEntry =
            mockCODEOWNERSContent.slice(0, configSectionIndex) +
            newContentBeforeConfig +
            "\n" +
            mockCODEOWNERSContent.slice(configSectionIndex);

        vi.mocked(fsModule.readFileSync).mockImplementation((path, options) => {
            if (path === mockCodeOwnersPath) {
                return contentWithExistingEntry;
            }
            if (path === mockIgnoreLinksPath) {
                return mockIgnoreLinksContent;
            }
            return "";
        }); // Call the function
        await codeOwnersModule.tryGenerateCodeOwnersAndIgnoreLinkForPackage(
            mockPackageFolderPath,
            mockPackageName
        );

        // Check that fs.writeFileSync was called only once (for ignore-links.txt)
        expect(fsModule.writeFileSync).toHaveBeenCalledTimes(2);

        // Check CODEOWNERS was updated but content didn't change
        expect(fsModule.writeFileSync).toHaveBeenNthCalledWith(
            1,
            mockCodeOwnersPath,
            contentWithExistingEntry,
        );
    });

    test("should not add duplicate entry to ignore-links.txt if link already exists", async () => {
        // Setup mock for tryGetNpmView to return undefined (package doesn't exist)
        vi.mocked(npmUtilsModule.tryGetNpmView).mockResolvedValue(undefined);

        // Create the new ignore link
        const newIgnoreLink = `https://learn.microsoft.com/javascript/api/${mockPackageName}?view=azure-node-preview`;

        // Modify mockIgnoreLinksContent to include the entry already
        const contentWithExistingLink =
            mockIgnoreLinksContent + "\n" + newIgnoreLink;

        vi.mocked(fsModule.readFileSync).mockImplementation((path, options) => {
            if (path === mockCodeOwnersPath) {
                return mockCODEOWNERSContent;
            }
            if (path === mockIgnoreLinksPath) {
                return contentWithExistingLink;
            } return "";
        });

        // Call the function
        await codeOwnersModule.tryGenerateCodeOwnersAndIgnoreLinkForPackage(
            mockPackageFolderPath,
            mockPackageName
        );

        // Check that fs.writeFileSync was called only once (for CODEOWNERS)
        expect(fsModule.writeFileSync).toHaveBeenCalledTimes(1);

        // Check only CODEOWNERS was updated
        expect(fsModule.writeFileSync).toHaveBeenNthCalledWith(
            1,
            mockCodeOwnersPath,
            expect.any(String),
        );

        // Check ignore-links.txt was not updated
        expect(fsModule.writeFileSync).not.toHaveBeenCalledWith(
            mockIgnoreLinksPath,
            expect.any(String),
        );
    });
});
