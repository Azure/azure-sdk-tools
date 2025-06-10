import fs from "fs";
import path from "path";
import shell from "shelljs";
import { logger } from "../../utils/logger.js";
import { SDKType } from "../types.js";
import { getNpmPackageName } from "../utils.js";
import { tryGetNpmView } from "../npmUtils.js";
import { getGeneratedPackageDirectory } from "../../common/utils.js";
import { posix } from "node:path";

export async function generateCodeOwnersAndIgnoreLink(
    sdkType: SDKType,
    options: {
        changedPackagePaths: string[];
        typespecProject?: string;
        typeSpecDirectory: string;
        sdkRepo: string;
    },
) {
    if(SDKType.ModularClient === sdkType) {
        logger.info(`Start with ModularClient.`);
        const typeSpecDirectory = path.posix.join(
            options.typeSpecDirectory,
            options.typespecProject!,
        );
        const packageDirectory = await getGeneratedPackageDirectory(
            typeSpecDirectory,
            options.sdkRepo.replaceAll("\\", "/"),
        );
        const relativePackageDirToSdkRoot = posix.relative(
            posix.normalize(options.sdkRepo.replaceAll("\\", "/")),
            posix.normalize(packageDirectory),
        );
        await generateCodeOwnersAndIgnoreLinkForPackage(
            relativePackageDirToSdkRoot,
        );
    } else {
        // Only for hlc.
        for (const packageFolderPath of options.changedPackagePaths) {
            logger.info(`packageFolderPath '${packageFolderPath}'`);
            await generateCodeOwnersAndIgnoreLinkForPackage(packageFolderPath);
        }
    }    
}

export async function generateCodeOwnersAndIgnoreLinkForPackage(
    packageFolderPath: string,
) {
    logger.info(
        `Start to generate CODEOWNERS and ignore link for ${packageFolderPath}`,
    );
    const jsSdkRepoPath = String(shell.pwd());
    const packageAbsolutePath = path.join(jsSdkRepoPath, packageFolderPath);
    const packageName = getNpmPackageName(packageAbsolutePath);
    const npmViewResult = await tryGetNpmView(packageName);
    logger.info(`npmViewResult: ${npmViewResult}`);
    if (!npmViewResult) {
        logger.info(
            `Package ${packageName} is first beta release, start to generate CODEOWNERS and ignore link for first beta release.`,
        );
        updateCODEOWNERS(packageFolderPath);
        updateIgnoreLink(packageName);
        logger.info(
            `Generated updates for CODEOWNERS and ignore link successfully`,
        );
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
}

function updateIgnoreLink(packageName: string) {
    const jsSdkRepoPath = String(shell.pwd());
    const ignoreLinksPath = path.join(jsSdkRepoPath, "eng", "ignore-links.txt");
    let content = fs.readFileSync(ignoreLinksPath, "utf8");
    const newLine = `https://learn.microsoft.com/javascript/api/${packageName}?view=azure-node-preview`;
    if (!content.endsWith("\n")) {
        content += "\n";
    }
    const updatedContent = content + newLine + "\n";
    fs.writeFileSync(ignoreLinksPath, updatedContent);
}
