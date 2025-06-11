import { generateCodeOwnersAndIgnoreLinkForPackage } from "../../common/codeownersAndignorelink/generateCodeOwnersAndIgnoreLink.js";
import { logger } from "../../utils/logger.js";
import { getChangedPackageDirectory } from "../../utils/git.js";


export const generateCodeOwnersAndIgnoreLink = async (
    skipGeneration: boolean,
): Promise<void> => {
    logger.info(
        `Generating CODEOWNERS and ignore link for HighLevelClient package.`,
    );
    const changedPackageDirectories: Set<string> =
        await getChangedPackageDirectory(!skipGeneration);
    logger.info(
        `changedPackageDirectories: ${Array.from(changedPackageDirectories)}`,
    );
    for (const packageFolderPath of changedPackageDirectories) {
        logger.info(`packageFolderPath '${packageFolderPath}'`);
        await generateCodeOwnersAndIgnoreLinkForPackage(packageFolderPath);
    }
};
