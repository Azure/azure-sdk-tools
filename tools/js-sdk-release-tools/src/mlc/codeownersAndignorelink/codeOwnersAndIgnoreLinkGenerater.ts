import { generateCodeOwnersAndIgnoreLinkForPackage } from "../../common/codeownersAndignorelink/generateCodeOwnersAndIgnoreLink.js";
import { logger } from "../../utils/logger.js";

export const generateCodeOwnersAndIgnoreLink = async (
    packages: any,
): Promise<void> => {
    logger.info(`Generating CODEOWNERS and ignore link for Modular client.`);
    logger.info(`MLC received packages: ${JSON.stringify(packages, null, 2)}`);

    // Extract packageFolder from packages
    const packageInfo = packages?.[0];
    if (!packageInfo || packageInfo.result !== "succeeded") {
        logger.warn(
            `Package ${packageInfo.packageName} generation result is not successful. Skipping CODEOWNERS and ignore link generation.`,
        );
        return;
    }

    if (!packageInfo.packageFolder) {
        logger.error(`Package folder not found for ${packageInfo.packageName || 'unknown package'}`);
        return;
    }
    logger.info(`Processing package: ${packageInfo.packageName} using folder: ${packageInfo.packageFolder}`);

    await generateCodeOwnersAndIgnoreLinkForPackage(
        packageInfo.packageFolder,
    );
};
