import { generateCodeOwnersAndIgnoreLinkForPackage } from "../../common/codeownersAndignorelink/generateCodeOwnersAndIgnoreLink.js";
import { logger } from "../../utils/logger.js";

export const generateCodeOwnersAndIgnoreLink = async (
    packages: any
): Promise<void> => {
    logger.info(
        `Generating CODEOWNERS and ignore link for HighLevelClient package.`,
    );
  
    // Process each package
    for (const pkg of packages) {   
        // Check if package generation was successful
        if (pkg.result !== "succeeded") {
            logger.warn(
                `Package ${pkg.packageName} generation result is not successful. Skipping CODEOWNERS and ignore link generation.`,
            );
            continue;
        }
        if (!pkg.packageFolder) {
            logger.error(`Package folder not found for ${pkg.packageName || 'unknown package'}`);
            continue;
        }
        logger.info(`Processing package: ${pkg.packageName} using folder: ${pkg.packageFolder}`);
        await generateCodeOwnersAndIgnoreLinkForPackage(pkg.packageFolder);
    }
};
