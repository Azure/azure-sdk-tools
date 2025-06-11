import { generateCodeOwnersAndIgnoreLinkForPackage } from "../../common/codeownersAndignorelink/generateCodeOwnersAndIgnoreLink.js";
import { logger } from "../../utils/logger.js";
import { getGeneratedPackageDirectory } from "../../common/utils.js";
import { posix } from "node:path";

export const generateCodeOwnersAndIgnoreLink = async (options: {
    typeSpecDirectory: string;
    sdkRepoRoot: string;
}): Promise<void> => {
    logger.info(`Generating CODEOWNERS and ignore link for Modular client.`);
    const packageDirectory = await getGeneratedPackageDirectory(
        options.typeSpecDirectory,
        options.sdkRepoRoot,
    );
    const relativePackageDirToSdkRoot = posix.relative(
        posix.normalize(options.sdkRepoRoot),
        posix.normalize(packageDirectory),
    );
    await generateCodeOwnersAndIgnoreLinkForPackage(
        relativePackageDirToSdkRoot,
    );
};
