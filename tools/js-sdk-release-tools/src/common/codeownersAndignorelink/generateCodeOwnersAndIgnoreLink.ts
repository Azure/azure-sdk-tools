import fs from "fs";
import path from "path";
import shell from "shelljs";
import { logger } from "../../utils/logger.js";
import { getNpmPackageName } from "../utils.js";
import { tryGetNpmView } from "../npmUtils.js";

import { getChangedPackageDirectory } from "../../utils/git.js"; // Corrected import path

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
    const isFirstPackageToNpm = npmViewResult === undefined;
    if (isFirstPackageToNpm) {
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
    
    // Check if the link already exists in the file
    if (content.includes(newLine)) {
        logger.info(`Link for ${packageName} already exists in ignore-links.txt, skipping.`);
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
