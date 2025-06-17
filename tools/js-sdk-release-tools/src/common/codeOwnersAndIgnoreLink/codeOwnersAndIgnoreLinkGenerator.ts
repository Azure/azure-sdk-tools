import fs from "fs";
import path from "path";
import shell from "shelljs";
import { logger } from "../../utils/logger.js";
import { getGeneratedPackageDirectory, getNpmPackageName, getPackageNameFromTspConfig } from "../utils.js";
import { tryGetNpmView } from "../npmUtils.js";
import { SDKType } from "../types.js";

export const codeOwnersAndIgnoreLinkGenerator = async (options: {
    sdkType: SDKType;
    typeSpecDirectory: string;
}): Promise<void> => {
    logger.info(`Generating CODEOWNERS and ignore link for packages`);

    // Only proceed for management + Modular clients
    if (options.sdkType !== SDKType.ModularClient) {
        logger.warn(
            `Unsupported SDK type ${options.sdkType} for CODEOWNERS and ignore link generation. Only ModularClient with management is supported.`,
        );
        return;
    }
    const packageDirectory = await getGeneratedPackageDirectory(options.typeSpecDirectory, '');

    if (!packageDirectory) {
        logger.warn("Failed to get package directory");
        return;
    }
    const packageName = await getPackageNameFromTspConfig(options.typeSpecDirectory);
    await tryGenerateCodeOwnersAndIgnoreLinkForPackage(packageDirectory, packageName);
};

export async function tryGenerateCodeOwnersAndIgnoreLinkForPackage(
    packageFolderPath: string,
    packageName?: string
) {
    logger.info(
        `Start to generate CODEOWNERS and ignore link for ${packageFolderPath}`,
    );
    let isFirstPackageToNpm = false;
    let pkgName: string = '';

    try {
        const jsSdkRepoPath = String(shell.pwd());
        const packageAbsolutePath = path.join(jsSdkRepoPath, packageFolderPath);
        pkgName = getNpmPackageName(packageAbsolutePath);
        const npmViewResult = await tryGetNpmView(pkgName);
        isFirstPackageToNpm = npmViewResult === undefined;
    } catch (error) {
        logger.info(
            `Failed to get NPM package name: ${error}. Treating as first package to NPM.`,
        );
        isFirstPackageToNpm = true;
    }

    // Use pkgName as a fallback if packageName is undefined
    const effectivePackageName = packageName || pkgName;

    if (isFirstPackageToNpm) {
        logger.info(`Package ${effectivePackageName} is first beta release, start to generate CODEOWNERS and ignore link for first beta release.`);
        updateCODEOWNERS(packageFolderPath);
        updateIgnoreLink(effectivePackageName);
        logger.info(`Generated updates for CODEOWNERS and ignore link successfully`);
    } else {
        logger.info(`Package ${effectivePackageName} is not first beta release, skipping CODEOWNERS and ignore link generation.`);
    }
}

function updateCODEOWNERS(packagePath: string) {
    const jsSdkRepoPath = String(shell.pwd());
    const codeownersPath = path.join(jsSdkRepoPath, ".github", "CODEOWNERS");
    let content = fs.readFileSync(codeownersPath, "utf8");

    // Insert content before Config section
    const configSectionIndex = content.indexOf(
        "###########\n# Config\n###########",
    );
    if (configSectionIndex !== -1) {
        const newContentBeforeConfig = `# PRLabel: %Mgmt\n${packagePath}/ @qiaozha @MaryGao\n`;
        if (!content.includes(newContentBeforeConfig)) {
            content =
                content.slice(0, configSectionIndex) +
                newContentBeforeConfig +
                "\n" +
                content.slice(configSectionIndex);
        }
    }
    fs.writeFileSync(codeownersPath, content);
    logger.info(`Updated CODEOWNERS for package: ${packagePath}`);
}

function updateIgnoreLink(packageName: string) {
    const jsSdkRepoPath = String(shell.pwd());
    const ignoreLinksPath = path.join(jsSdkRepoPath, "eng", "ignore-links.txt");
    let content = fs.readFileSync(ignoreLinksPath, "utf8");
    const newLine = `https://learn.microsoft.com/javascript/api/${packageName}?view=azure-node-preview`;

    // Check if the link already exists in the file
    if (content.includes(newLine)) {
        logger.warn(`Failed to add link for ${packageName} to ignore-links.txt as it already exists, skipping.`);
        return;
    }

    // Ensure the content ends with a newline
    if (!content.endsWith("\n")) {
        content += "\n";
    }

    const updatedContent = content + newLine + "\n";
    fs.writeFileSync(ignoreLinksPath, updatedContent);
    logger.info(`Added link for ${packageName} to ignore-links.txt`);
}
